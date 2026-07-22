namespace UrlShortener.Models;

/// <summary>Data shown on the per-link statistics page.</summary>
public class StatsViewModel
{
    public ShortUrl Url { get; set; } = default!;
    public int TotalClicks { get; set; }

    /// <summary>Day labels (e.g. "07-14") for the clicks-over-time chart.</summary>
    public List<string> DayLabels { get; set; } = new();

    /// <summary>Click counts aligned with <see cref="DayLabels"/>.</summary>
    public List<int> DayCounts { get; set; } = new();

    public List<Click> RecentClicks { get; set; } = new();
    public List<(string Referrer, int Count)> TopReferrers { get; set; } = new();
}
