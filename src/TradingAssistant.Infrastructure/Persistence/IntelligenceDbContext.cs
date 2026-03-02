using Microsoft.EntityFrameworkCore;
using TradingAssistant.Domain.Intelligence;

namespace TradingAssistant.Infrastructure.Persistence;

public class IntelligenceDbContext : DbContext
{
    public IntelligenceDbContext(DbContextOptions<IntelligenceDbContext> options) : base(options) { }

    public DbSet<MarketRegime> MarketRegimes => Set<MarketRegime>();
    public DbSet<RegimeTransition> RegimeTransitions => Set<RegimeTransition>();
    public DbSet<MarketProfile> MarketProfiles => Set<MarketProfile>();
    public DbSet<BreadthSnapshot> BreadthSnapshots => Set<BreadthSnapshot>();
    public DbSet<CorrelationSnapshot> CorrelationSnapshots => Set<CorrelationSnapshot>();
    public DbSet<CostProfile> CostProfiles => Set<CostProfile>();
    public DbSet<PipelineRunLog> PipelineRunLogs => Set<PipelineRunLog>();
    public DbSet<CircuitBreakerEvent> CircuitBreakerEvents => Set<CircuitBreakerEvent>();
    public DbSet<TournamentEntry> TournamentEntries => Set<TournamentEntry>();
    public DbSet<StrategyAssignment> StrategyAssignments => Set<StrategyAssignment>();
    public DbSet<StrategyRegimeScore> StrategyRegimeScores => Set<StrategyRegimeScore>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MarketRegime>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MarketCode).HasMaxLength(30).IsRequired();
            entity.Property(e => e.SmaSlope50).HasPrecision(18, 6);
            entity.Property(e => e.SmaSlope200).HasPrecision(18, 6);
            entity.Property(e => e.VixLevel).HasPrecision(8, 2);
            entity.Property(e => e.BreadthScore).HasPrecision(8, 4);
            entity.Property(e => e.PctAbove200Sma).HasPrecision(8, 4);
            entity.Property(e => e.AdvanceDeclineRatio).HasPrecision(8, 4);
            entity.Property(e => e.ConfidenceScore).HasPrecision(8, 4);
            entity.HasIndex(e => e.MarketCode);
            entity.HasIndex(e => e.ClassifiedAt);
        });

        modelBuilder.Entity<RegimeTransition>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MarketCode).HasMaxLength(30).IsRequired();
            entity.Property(e => e.SmaSlope50).HasPrecision(18, 6);
            entity.Property(e => e.SmaSlope200).HasPrecision(18, 6);
            entity.Property(e => e.VixLevel).HasPrecision(8, 2);
            entity.Property(e => e.BreadthScore).HasPrecision(8, 4);
            entity.Property(e => e.PctAbove200Sma).HasPrecision(8, 4);
            entity.HasIndex(e => e.MarketCode);
            entity.HasIndex(e => e.TransitionDate);
        });

        modelBuilder.Entity<MarketProfile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MarketCode).HasMaxLength(30).IsRequired();
            entity.HasIndex(e => e.MarketCode).IsUnique();
            entity.Property(e => e.Exchange).HasMaxLength(50);
            entity.Property(e => e.Currency).HasMaxLength(3);
            entity.Property(e => e.Timezone).HasMaxLength(50);
            entity.Property(e => e.VixSymbol).HasMaxLength(20);
            entity.Property(e => e.DataSource).HasMaxLength(30);
            entity.Property(e => e.ConfigJson).HasMaxLength(8_000);
            entity.Property(e => e.DnaProfileJson).HasMaxLength(16_000);

            entity.HasData(
                new MarketProfile
                {
                    Id = Guid.Parse("a1b2c3d4-0001-0001-0001-000000000001"),
                    MarketCode = "US_SP500",
                    Exchange = "NYSE/NASDAQ",
                    Currency = "USD",
                    Timezone = "America/New_York",
                    VixSymbol = "^VIX",
                    DataSource = "yahoo",
                    ConfigJson = """{"tradingHours":{"open":"09:30","close":"16:00"},"regimeThresholds":{"highVol":30,"bullBreadth":0.60,"bearBreadth":0.40}}""",
                    CreatedAt = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new MarketProfile
                {
                    Id = Guid.Parse("a1b2c3d4-0001-0001-0001-000000000002"),
                    MarketCode = "IN_NIFTY50",
                    Exchange = "NSE",
                    Currency = "INR",
                    Timezone = "Asia/Kolkata",
                    VixSymbol = "^INDIAVIX",
                    DataSource = "yahoo",
                    ConfigJson = """{"tradingHours":{"open":"09:15","close":"15:30"},"regimeThresholds":{"highVol":25,"bullBreadth":0.55,"bearBreadth":0.45}}""",
                    CreatedAt = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            );
        });

        modelBuilder.Entity<BreadthSnapshot>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MarketCode).HasMaxLength(30).IsRequired();
            entity.Property(e => e.AdvanceDeclineRatio).HasPrecision(8, 4);
            entity.Property(e => e.PctAbove200Sma).HasPrecision(8, 4);
            entity.Property(e => e.PctAbove50Sma).HasPrecision(8, 4);
            entity.HasIndex(e => new { e.MarketCode, e.SnapshotDate });
        });

        modelBuilder.Entity<CorrelationSnapshot>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MatrixJson).HasMaxLength(8_000);
            entity.HasIndex(e => e.SnapshotDate);
        });

        modelBuilder.Entity<CostProfile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MarketCode).HasMaxLength(30).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.CommissionPerShare).HasPrecision(18, 6);
            entity.Property(e => e.CommissionPercent).HasPrecision(8, 4);
            entity.Property(e => e.ExchangeFeePercent).HasPrecision(8, 4);
            entity.Property(e => e.TaxPercent).HasPrecision(8, 4);
            entity.Property(e => e.SpreadEstimatePercent).HasPrecision(8, 4);
            entity.HasIndex(e => new { e.MarketCode, e.IsActive });

            entity.HasData(
                new CostProfile
                {
                    Id = Guid.Parse("b2c3d4e5-0001-0001-0001-000000000001"),
                    MarketCode = "US_SP500",
                    Name = "US Equities (default)",
                    CommissionPerShare = 0.005m,
                    CommissionPercent = 0m,
                    ExchangeFeePercent = 0m,
                    TaxPercent = 0m,
                    SpreadEstimatePercent = 0.1m,
                    IsActive = true
                },
                new CostProfile
                {
                    Id = Guid.Parse("b2c3d4e5-0001-0001-0001-000000000002"),
                    MarketCode = "IN_NIFTY50",
                    Name = "India Equities (default)",
                    CommissionPerShare = 0m,
                    CommissionPercent = 0.03m,
                    ExchangeFeePercent = 0m,
                    TaxPercent = 0.025m,
                    SpreadEstimatePercent = 0.05m,
                    IsActive = true
                }
            );
        });

        modelBuilder.Entity<PipelineRunLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MarketCode).HasMaxLength(30).IsRequired();
            entity.Property(e => e.StepName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.ErrorMessage).HasMaxLength(2000);
            entity.HasIndex(e => new { e.MarketCode, e.RunDate });
        });

        modelBuilder.Entity<CircuitBreakerEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AccountId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.EventType).HasMaxLength(20).IsRequired();
            entity.Property(e => e.PeakEquity).HasPrecision(18, 2);
            entity.Property(e => e.CurrentEquity).HasPrecision(18, 2);
            entity.Property(e => e.DrawdownPercent).HasPrecision(8, 4);
            entity.Property(e => e.ThresholdPercent).HasPrecision(8, 4);
            entity.Property(e => e.RegimeAtEvent).HasMaxLength(30);
            entity.Property(e => e.RegimeConfidence).HasPrecision(8, 4);
            entity.Property(e => e.DeactivationReason).HasMaxLength(200);
            entity.HasIndex(e => new { e.AccountId, e.EventDate });
        });

        modelBuilder.Entity<TournamentEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MarketCode).HasMaxLength(30).IsRequired();
            entity.Property(e => e.WinRate).HasPrecision(8, 4);
            entity.Property(e => e.SharpeRatio).HasPrecision(8, 4);
            entity.Property(e => e.MaxDrawdown).HasPrecision(8, 4);
            entity.Property(e => e.TotalReturn).HasPrecision(18, 4);
            entity.Property(e => e.AllocationPercent).HasPrecision(8, 2);
            entity.Property(e => e.RetirementReason).HasMaxLength(500);
            entity.HasIndex(e => new { e.Status, e.MarketCode });
            entity.HasIndex(e => e.StrategyId);
        });

        modelBuilder.Entity<StrategyAssignment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MarketCode).HasMaxLength(30).IsRequired();
            entity.Property(e => e.StrategyName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.AllocationPercent).HasPrecision(8, 2);
            entity.HasIndex(e => e.MarketCode).IsUnique();
        });

        modelBuilder.Entity<StrategyRegimeScore>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MarketCode).HasMaxLength(30).IsRequired();
            entity.Property(e => e.SharpeRatio).HasPrecision(8, 4);
            entity.HasIndex(e => new { e.StrategyId, e.MarketCode, e.Regime }).IsUnique();
        });
    }
}
