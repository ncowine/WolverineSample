using Microsoft.EntityFrameworkCore;
using TradingAssistant.Domain.Backtesting;

namespace TradingAssistant.Infrastructure.Persistence;

public class BacktestDbContext : DbContext
{
    public BacktestDbContext(DbContextOptions<BacktestDbContext> options) : base(options) { }

    public DbSet<Strategy> Strategies => Set<Strategy>();
    public DbSet<StrategyRule> StrategyRules => Set<StrategyRule>();
    public DbSet<BacktestRun> BacktestRuns => Set<BacktestRun>();
    public DbSet<BacktestResult> BacktestResults => Set<BacktestResult>();
    public DbSet<OptimizedParameterSet> OptimizedParameterSets => Set<OptimizedParameterSet>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Strategy>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.RulesJson).HasMaxLength(8000);
            entity.Ignore(e => e.UsesV2Engine);
        });

        modelBuilder.Entity<StrategyRule>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Condition).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Threshold).HasPrecision(18, 6);
            entity.HasOne(e => e.Strategy)
                .WithMany(s => s.Rules)
                .HasForeignKey(e => e.StrategyId);
        });

        modelBuilder.Entity<BacktestRun>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Symbol).HasMaxLength(10).IsRequired();
            entity.HasOne(e => e.Strategy)
                .WithMany(s => s.BacktestRuns)
                .HasForeignKey(e => e.StrategyId);
        });

        modelBuilder.Entity<BacktestResult>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.WinRate).HasPrecision(8, 4);
            entity.Property(e => e.TotalReturn).HasPrecision(18, 4);
            entity.Property(e => e.MaxDrawdown).HasPrecision(18, 4);
            entity.Property(e => e.SharpeRatio).HasPrecision(8, 4);
            entity.Property(e => e.Cagr).HasPrecision(18, 4);
            entity.Property(e => e.SortinoRatio).HasPrecision(8, 4);
            entity.Property(e => e.CalmarRatio).HasPrecision(8, 4);
            entity.Property(e => e.ProfitFactor).HasPrecision(8, 4);
            entity.Property(e => e.Expectancy).HasPrecision(18, 4);
            entity.Property(e => e.OverfittingScore).HasPrecision(8, 4);
            entity.Property(e => e.EquityCurveJson).HasMaxLength(500_000);
            entity.Property(e => e.TradeLogJson).HasMaxLength(500_000);
            entity.Property(e => e.MonthlyReturnsJson).HasMaxLength(8_000);
            entity.Property(e => e.BenchmarkReturnJson).HasMaxLength(500_000);
            entity.Property(e => e.ParametersJson).HasMaxLength(4_000);
            entity.Property(e => e.WalkForwardJson).HasMaxLength(100_000);
            entity.Property(e => e.SpyComparisonJson).HasMaxLength(1_000);
            entity.HasOne(e => e.BacktestRun)
                .WithOne(r => r.Result)
                .HasForeignKey<BacktestResult>(e => e.BacktestRunId);
        });

        modelBuilder.Entity<OptimizedParameterSet>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ParametersJson).HasMaxLength(4_000).IsRequired();
            entity.Property(e => e.OverfittingGrade).HasMaxLength(20).IsRequired();
            entity.Property(e => e.AvgOutOfSampleSharpe).HasPrecision(8, 4);
            entity.Property(e => e.AvgEfficiency).HasPrecision(8, 4);
            entity.Property(e => e.AvgOverfittingScore).HasPrecision(8, 4);
            entity.HasIndex(e => new { e.StrategyId, e.IsActive });
            entity.HasOne(e => e.Strategy)
                .WithMany()
                .HasForeignKey(e => e.StrategyId);
        });
    }
}
