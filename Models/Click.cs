namespace UrlShortener.Models;

/// <summary>A single visit to a shortened link, recorded for analytics.</summary>
public class Click
{
    public int Id { get; set; }

    public int ShortUrlId { get; set; }
    public ShortUrl? ShortUrl { get; set; }

    public DateTime ClickedAt { get; set; } = DateTime.UtcNow;

    public string? Referrer { get; set; }
    public string? UserAgent { get; set; }
}
