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

    public HomeController(AppDbContext db) => _db = db;

    // GET / — list of links + the create form.
    public async Task<IActionResult> Index()
    {
        var links = await _db.ShortUrls
            .Include(u => u.Clicks)
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();

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

        _db.ShortUrls.Add(new ShortUrl { Code = code, OriginalUrl = originalUrl });
        await _db.SaveChangesAsync();

        TempData["NewCode"] = code;
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
        var link = await _db.ShortUrls
            .Include(u => u.Clicks)
            .FirstOrDefaultAsync(u => u.Id == id);
        if (link == null) return NotFound();

        // Clicks per day for the last 14 days.
        var today = DateTime.UtcNow.Date;
        var labels = new List<string>();
        var counts = new List<int>();
        for (int i = 13; i >= 0; i--)
        {
            var day = today.AddDays(-i);
            labels.Add(day.ToString("MM-dd"));
            counts.Add(link.Clicks.Count(c => c.ClickedAt.Date == day));
        }

        var topReferrers = link.Clicks
            .GroupBy(c => string.IsNullOrEmpty(c.Referrer) ? "Direct / unknown" : c.Referrer!)
            .Select(g => (Referrer: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .Take(5)
            .ToList();

        var vm = new StatsViewModel
        {
            Url = link,
            TotalClicks = link.Clicks.Count,
            DayLabels = labels,
            DayCounts = counts,
            RecentClicks = link.Clicks.OrderByDescending(c => c.ClickedAt).Take(10).ToList(),
            TopReferrers = topReferrers
        };
        return View(vm);
    }

    // POST /Home/Delete/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
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

    private static string RandomCode(int length)
    {
        var bytes = RandomNumberGenerator.GetBytes(length);
        var chars = new char[length];
        for (int i = 0; i < length; i++)
            chars[i] = Alphabet[bytes[i] % Alphabet.Length];
        return new string(chars);
    }
}
