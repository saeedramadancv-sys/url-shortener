using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UrlShortener.Data;
using UrlShortener.Models;

namespace UrlShortener.Controllers;

public class HomeController : Controller
{
    // Ambiguous-looking characters (0/O, 1/l/I) are omitted on purpose.
    private const string Alphabet = "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private readonly AppDbContext _db;
    private readonly bool _allowDelete;

    public HomeController(AppDbContext db, IConfiguration configuration)
    {
        _db = db;

        // The public demo has no accounts, so anyone could delete its sample links. Seeded
        // data means it's the demo, so delete is off there unless a setting says otherwise.
        _allowDelete = configuration.GetValue("AllowDelete", !configuration.GetValue<bool>("Seed"));
    }

    // GET / — list of links + the create form.
    public async Task<IActionResult> Index()
    {
        // Clicks.Count here becomes a COUNT in SQL, so the page doesn't load every click.
        var links = await _db.ShortUrls
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new LinkRow(u.Id, u.Code, u.OriginalUrl, u.CreatedAt, u.Clicks.Count))
            .ToListAsync();

        ViewBag.AllowDelete = _allowDelete;
        return View(links);
    }

    // POST /Home/Create — shorten a URL.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string originalUrl, string? customAlias)
    {
        originalUrl = (originalUrl ?? "").Trim();

        if (!IsValidHttpUrl(originalUrl))
        {
            TempData["Error"] = "Please enter a valid URL starting with http:// or https://";
            return RedirectToAction(nameof(Index));
        }

        string code;
        if (!string.IsNullOrWhiteSpace(customAlias))
        {
            customAlias = customAlias.Trim();
            if (!System.Text.RegularExpressions.Regex.IsMatch(customAlias, "^[A-Za-z0-9_-]{3,32}$"))
            {
                TempData["Error"] = "Custom alias must be 3-32 characters (letters, numbers, - or _).";
                return RedirectToAction(nameof(Index));
            }
            if (await _db.ShortUrls.AnyAsync(u => u.Code == customAlias))
            {
                TempData["Error"] = $"The alias '{customAlias}' is already taken.";
                return RedirectToAction(nameof(Index));
            }
            code = customAlias;
        }
        else
        {
            code = await GenerateUniqueCodeAsync();
        }

        var link = new ShortUrl { Code = code, OriginalUrl = originalUrl };
        for (var attempt = 1; ; attempt++)
        {
            _db.ShortUrls.Add(link);
            try
            {
                await _db.SaveChangesAsync();
                break;
            }
            catch (DbUpdateException)
            {
                // The AnyAsync checks above can't see a request that saves the same code a
                // moment later; the unique index on Code is what actually stops the duplicate.
                // Turn that into a normal answer instead of an error page.
                _db.Entry(link).State = EntityState.Detached;
                if (!await _db.ShortUrls.AnyAsync(u => u.Code == link.Code))
                    throw;

                if (!string.IsNullOrWhiteSpace(customAlias) || attempt == 3)
                {
                    TempData["Error"] = string.IsNullOrWhiteSpace(customAlias)
                        ? "Could not create a short link, please try again."
                        : $"The alias '{customAlias}' is already taken.";
                    return RedirectToAction(nameof(Index));
                }

                link.Code = await GenerateUniqueCodeAsync();
            }
        }

        TempData["NewCode"] = link.Code;
        return RedirectToAction(nameof(Index));
    }

    // GET /r/{code} — redirect to the original URL and record the click.
    [HttpGet]
    public async Task<IActionResult> Go(string code)
    {
        var link = await _db.ShortUrls.FirstOrDefaultAsync(u => u.Code == code);
        if (link == null) return NotFound();

        var referrer = Request.Headers.Referer.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();
        _db.Clicks.Add(new Click
        {
            ShortUrlId = link.Id,
            Referrer = string.IsNullOrWhiteSpace(referrer) ? null : referrer,
            UserAgent = string.IsNullOrWhiteSpace(userAgent) ? null : userAgent[..Math.Min(userAgent.Length, 400)]
        });
        await _db.SaveChangesAsync();

        return Redirect(link.OriginalUrl);
    }

    // GET /Home/Details/{id} — statistics for a single link.
    public async Task<IActionResult> Details(int id)
    {
        var link = await _db.ShortUrls.FirstOrDefaultAsync(u => u.Id == id);
        if (link == null) return NotFound();

        // All the grouping happens in SQL, so a link with a lot of clicks stays cheap to open.
        var clicks = _db.Clicks.Where(c => c.ShortUrlId == id);

        // Clicks per day for the last 14 days.
        var today = DateTime.UtcNow.Date;
        var firstDay = today.AddDays(-13);
        var perDay = await clicks
            .Where(c => c.ClickedAt >= firstDay)
            .GroupBy(c => c.ClickedAt.Date)
            .Select(g => new { Day = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Day, x => x.Count);

        var labels = new List<string>();
        var counts = new List<int>();
        for (int i = 13; i >= 0; i--)
        {
            var day = today.AddDays(-i);
            labels.Add(day.ToString("MM-dd"));
            counts.Add(perDay.GetValueOrDefault(day));
        }

        var topReferrers = await clicks
            .GroupBy(c => string.IsNullOrEmpty(c.Referrer) ? "Direct / unknown" : c.Referrer!)
            .Select(g => new { Referrer = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .ToListAsync();

        var vm = new StatsViewModel
        {
            Url = link,
            TotalClicks = await clicks.CountAsync(),
            DayLabels = labels,
            DayCounts = counts,
            RecentClicks = await clicks.OrderByDescending(c => c.ClickedAt).Take(10).ToListAsync(),
            TopReferrers = topReferrers.Select(x => (x.Referrer, x.Count)).ToList()
        };
        return View(vm);
    }

    // POST /Home/Delete/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        if (!_allowDelete)
        {
            TempData["Error"] = "Deleting links is turned off on this demo.";
            return RedirectToAction(nameof(Index));
        }

        var link = await _db.ShortUrls.FindAsync(id);
        if (link != null)
        {
            _db.ShortUrls.Remove(link);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View();

    // ---- helpers ----
    private static bool IsValidHttpUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private async Task<string> GenerateUniqueCodeAsync(int length = 6)
    {
        while (true)
        {
            var code = RandomCode(length);
            if (!await _db.ShortUrls.AnyAsync(u => u.Code == code))
                return code;
        }
    }

    // GetItems picks each character uniformly. Taking a random byte modulo 57 did not:
    // 256 isn't a multiple of 57, so the first 28 characters came up more often.
    private static string RandomCode(int length) =>
        new(RandomNumberGenerator.GetItems<char>(Alphabet, length));
}
