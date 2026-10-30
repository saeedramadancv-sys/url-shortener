using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UrlShortener.Data;
using UrlShortener.Models;
using Xunit;

namespace UrlShortener.Tests;

public class ShortLinkTests(ShortLinkFactory factory) : IClassFixture<ShortLinkFactory>
{
    private HttpClient NewClient() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private async Task<T> WithDb<T>(Func<AppDbContext, Task<T>> action)
    {
        using var scope = factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    [Fact]
    public async Task ARedirectGoesToTheOriginalUrlAndRecordsTheClick()
    {
        var client = NewClient();
        await Forms.CreateLink(client, "https://example.com/a", "redirect-me");

        var response = await client.GetAsync("/r/redirect-me");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("https://example.com/a", response.Headers.Location!.ToString());
        Assert.Equal(1, await WithDb(db => db.Clicks.CountAsync(c => c.ShortUrl!.Code == "redirect-me")));
    }

    [Fact]
    public async Task AnUnknownCodeIsNotFound()
    {
        var response = await NewClient().GetAsync("/r/nothing-here");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://example.com/file")]
    [InlineData("not a url")]
    public async Task OnlyHttpAndHttpsLinksAreAccepted(string url)
    {
        var alias = $"bad-{Guid.NewGuid():N}"[..20];

        await Forms.CreateLink(NewClient(), url, alias);

        Assert.False(await WithDb(db => db.ShortUrls.AnyAsync(u => u.Code == alias)));
    }

    [Fact]
    public async Task AnAliasThatIsAlreadyTakenKeepsTheFirstLink()
    {
        var client = NewClient();
        await Forms.CreateLink(client, "https://example.com/first", "taken-alias");
        await Forms.CreateLink(client, "https://example.com/second", "taken-alias");

        var links = await WithDb(db => db.ShortUrls.Where(u => u.Code == "taken-alias").ToListAsync());

        Assert.Equal("https://example.com/first", Assert.Single(links).OriginalUrl);
    }

    [Fact]
    public async Task AFormPostWithoutTheAntiForgeryTokenIsRefused()
    {
        var response = await NewClient().PostAsync("/Home/Create", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["originalUrl"] = "https://example.com" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TheStatsPageCountsAllClicksButChartsOnlyTheLast14Days()
    {
        var id = await WithDb(async db =>
        {
            var link = new ShortUrl { Code = "stats-link", OriginalUrl = "https://example.com" };
            db.ShortUrls.Add(link);
            await db.SaveChangesAsync();
            db.Clicks.AddRange(
                new Click { ShortUrlId = link.Id, Referrer = "https://a.example" },
                new Click { ShortUrlId = link.Id, Referrer = "https://a.example" },
                new Click { ShortUrlId = link.Id },
                new Click { ShortUrlId = link.Id, ClickedAt = DateTime.UtcNow.AddDays(-20) });
            await db.SaveChangesAsync();
            return link.Id;
        });

        var html = await NewClient().GetStringAsync($"/Home/Details/{id}");

        var stats = Regex.Matches(html, "stat-value\">([^<]+)<").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal("4", stats[0]);
        Assert.Equal("3", stats[1]);
        Assert.Equal("2", stats[2]);
    }

    [Fact]
    public async Task DeleteRemovesTheLinkWhenItIsAllowed()
    {
        var client = NewClient();
        await Forms.CreateLink(client, "https://example.com", "delete-me");
        var id = await WithDb(db => db.ShortUrls.Where(u => u.Code == "delete-me").Select(u => u.Id).SingleAsync());

        await Forms.Delete(client, id);

        Assert.False(await WithDb(db => db.ShortUrls.AnyAsync(u => u.Id == id)));
    }

    [Fact]
    public async Task TheHealthEndpointChecksTheDatabase()
    {
        var response = await NewClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}

public class ReadOnlyDemoTests(ReadOnlyShortLinkFactory factory) : IClassFixture<ReadOnlyShortLinkFactory>
{
    [Fact]
    public async Task WithDeleteOffTheButtonIsHiddenAndThePostDoesNothing()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await Forms.CreateLink(client, "https://example.com", "keep-me");

        int id;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            id = await db.ShortUrls.Where(u => u.Code == "keep-me").Select(u => u.Id).SingleAsync();
        }

        await Forms.Delete(client, id);

        var html = await client.GetStringAsync("/");
        Assert.Contains("/r/keep-me", html);
        Assert.DoesNotContain("/Home/Delete", html);
    }
}

internal static class Forms
{
    public static async Task<string> Token(HttpClient client)
    {
        var html = await client.GetStringAsync("/");
        return Regex.Match(html, "__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
    }

    public static async Task CreateLink(HttpClient client, string url, string alias) =>
        await client.PostAsync("/Home/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["originalUrl"] = url,
            ["customAlias"] = alias,
            ["__RequestVerificationToken"] = await Token(client)
        }));

    public static async Task Delete(HttpClient client, int id) =>
        await client.PostAsync($"/Home/Delete/{id}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await Token(client)
        }));
}
