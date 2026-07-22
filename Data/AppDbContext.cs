using Microsoft.EntityFrameworkCore;
using UrlShortener.Models;

namespace UrlShortener.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<ShortUrl> ShortUrls => Set<ShortUrl>();
    public DbSet<Click> Clicks => Set<Click>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Short codes must be unique so a code maps to exactly one link.
        modelBuilder.Entity<ShortUrl>()
            .HasIndex(u => u.Code)
            .IsUnique();

        // Deleting a link removes its click history.
        modelBuilder.Entity<Click>()
            .HasOne(c => c.ShortUrl)
            .WithMany(u => u.Clicks)
            .HasForeignKey(c => c.ShortUrlId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
