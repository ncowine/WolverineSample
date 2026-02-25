using Microsoft.EntityFrameworkCore;
using TradingAssistant.Domain.MarketData;

namespace TradingAssistant.Infrastructure.Persistence;

public class MarketDataDbContext : DbContext
{
    public MarketDataDbContext(DbContextOptions<MarketDataDbContext> options) : base(options) { }

    public DbSet<Stock> Stocks => Set<Stock>();
    public DbSet<PriceCandle> PriceCandles => Set<PriceCandle>();
    public DbSet<TechnicalIndicator> TechnicalIndicators => Set<TechnicalIndicator>();

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
            entity.HasIndex(e => new { e.StockId, e.Timestamp, e.Interval });
        });

        modelBuilder.Entity<TechnicalIndicator>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Value).HasPrecision(18, 6);
            entity.HasOne(e => e.Stock)
                .WithMany(s => s.TechnicalIndicators)
                .HasForeignKey(e => e.StockId);
        });
    }
}
