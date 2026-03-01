using Microsoft.EntityFrameworkCore;
using TradingAssistant.Domain.MarketData;

namespace TradingAssistant.Infrastructure.Persistence;

public class MarketDataDbContext : DbContext
{
    public MarketDataDbContext(DbContextOptions<MarketDataDbContext> options) : base(options) { }

    public DbSet<Stock> Stocks => Set<Stock>();
    public DbSet<PriceCandle> PriceCandles => Set<PriceCandle>();
    public DbSet<TechnicalIndicator> TechnicalIndicators => Set<TechnicalIndicator>();
    public DbSet<Watchlist> Watchlists => Set<Watchlist>();
    public DbSet<WatchlistItem> WatchlistItems => Set<WatchlistItem>();
    public DbSet<StockUniverse> StockUniverses => Set<StockUniverse>();
    public DbSet<ScreenerRun> ScreenerRuns => Set<ScreenerRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Stock>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Symbol).IsUnique();
            entity.Property(e => e.Symbol).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Exchange).HasMaxLength(50);
            entity.Property(e => e.Sector).HasMaxLength(100);
        });

        modelBuilder.Entity<PriceCandle>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Open).HasPrecision(18, 4);
            entity.Property(e => e.High).HasPrecision(18, 4);
            entity.Property(e => e.Low).HasPrecision(18, 4);
            entity.Property(e => e.Close).HasPrecision(18, 4);
            entity.HasOne(e => e.Stock)
                .WithMany(s => s.PriceCandles)
                .HasForeignKey(e => e.StockId);
            entity.HasIndex(e => new { e.StockId, e.Interval, e.Timestamp }).IsUnique();
        });

        modelBuilder.Entity<TechnicalIndicator>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Value).HasPrecision(18, 6);
            entity.HasOne(e => e.Stock)
                .WithMany(s => s.TechnicalIndicators)
                .HasForeignKey(e => e.StockId);
        });

        modelBuilder.Entity<Watchlist>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(e => e.UserId);
        });

        modelBuilder.Entity<WatchlistItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Symbol).HasMaxLength(10).IsRequired();
            entity.HasOne(e => e.Watchlist)
                .WithMany(w => w.Items)
                .HasForeignKey(e => e.WatchlistId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.WatchlistId, e.Symbol }).IsUnique();
        });

        modelBuilder.Entity<StockUniverse>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Symbols).HasMaxLength(4000);
        });

        modelBuilder.Entity<ScreenerRun>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ScanDate);
            entity.Property(e => e.StrategyName).HasMaxLength(200);
            entity.Property(e => e.ResultsJson).HasMaxLength(500_000);
            entity.Property(e => e.WarningsJson).HasMaxLength(8_000);
        });
    }
}
