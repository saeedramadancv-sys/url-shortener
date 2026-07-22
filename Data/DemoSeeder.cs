using UrlShortener.Models;

namespace UrlShortener.Data;

// TEMPORARY: seeds demo data for screenshots. Runs only when "Seed": true.
public static class DemoSeeder
{
    public static void Seed(AppDbContext db)
    {
        if (db.ShortUrls.Any()) return;

        var links = new[]
        {
            new ShortUrl { Code = "portfolio", OriginalUrl = "https://github.com/saeedramadancv-sys" },
            new ShortUrl { Code = "quran",     OriginalUrl = "https://saeedramadancv-sys.github.io/quranverse/" },
            new ShortUrl { Code = "aspnet",    OriginalUrl = "https://learn.microsoft.com/aspnet/core" },
            new ShortUrl { Code = "aB3xY9",    OriginalUrl = "https://example.com/some/really/long/marketing/link/2026" },
        };
        db.ShortUrls.AddRange(links);
        db.SaveChanges();

        var refs = new[] { "https://www.linkedin.com/", "https://twitter.com/", "https://www.google.com/", null, "https://t.co/", null, "https://github.com/" };
        var rnd = new Random(42);
        var today = DateTime.UtcNow.Date;

        // Featured link: rich 14-day history.
        var featured = links[0];
        for (int d = 13; d >= 0; d--)
        {
            int n = rnd.Next(1, 8) + (d < 5 ? 5 : 0);
            for (int k = 0; k < n; k++)
                db.Clicks.Add(new Click
                {
                    ShortUrlId = featured.Id,
                    ClickedAt = today.AddDays(-d).AddHours(rnd.Next(0, 24)).AddMinutes(rnd.Next(0, 60)),
                    Referrer = refs[rnd.Next(refs.Length)]
                });
        }
        foreach (var l in links.Skip(1))
        {
            int n = rnd.Next(4, 16);
            for (int k = 0; k < n; k++)
                db.Clicks.Add(new Click
                {
                    ShortUrlId = l.Id,
                    ClickedAt = today.AddDays(-rnd.Next(0, 14)).AddHours(rnd.Next(0, 24)),
                    Referrer = refs[rnd.Next(refs.Length)]
                });
        }
        db.SaveChanges();
    }
}
