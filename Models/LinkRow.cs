namespace UrlShortener.Models;

/// <summary>One row of the links table, with its click total worked out by the database.</summary>
public record LinkRow(int Id, string Code, string OriginalUrl, DateTime CreatedAt, int ClickCount);
