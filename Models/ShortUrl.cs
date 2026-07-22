using System.ComponentModel.DataAnnotations;

namespace UrlShortener.Models;

/// <summary>A shortened link and its click history.</summary>
public class ShortUrl
{
    public int Id { get; set; }

    /// <summary>The short code used in the redirect URL (e.g. "aB3xY9").</summary>
    [Required, MaxLength(32)]
    public string Code { get; set; } = "";

    [Required, MaxLength(2048)]
    public string OriginalUrl { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Click> Clicks { get; set; } = new();
}
