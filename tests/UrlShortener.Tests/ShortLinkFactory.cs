using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace UrlShortener.Tests;

/// <summary>
/// Boots the real app on an in-memory SQLite database. It's real SQL, so the unique index
/// and the GROUP BY queries run for real. The kept-open connection keeps the database alive.
/// </summary>
public class ShortLinkFactory : WebApplicationFactory<Program>
{
    private readonly string _name = $"shortlink-{Guid.NewGuid():N}";
    private SqliteConnection? _keepAlive;

    protected virtual string AllowDelete => "true";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connectionString = $"Data Source={_name};Mode=Memory;Cache=Shared";
        _keepAlive = new SqliteConnection(connectionString);
        _keepAlive.Open();

        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UseSqlite"] = "true",
                ["ConnectionStrings:Sqlite"] = connectionString,
                ["AllowDelete"] = AllowDelete
            }));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _keepAlive?.Dispose();
    }
}

public class ReadOnlyShortLinkFactory : ShortLinkFactory
{
    protected override string AllowDelete => "false";
}
