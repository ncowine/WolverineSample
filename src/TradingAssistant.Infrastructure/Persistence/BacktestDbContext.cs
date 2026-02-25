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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Strategy>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1000);
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
            entity.HasOne(e => e.BacktestRun)
                .WithOne(r => r.Result)
                .HasForeignKey<BacktestResult>(e => e.BacktestRunId);
        });
    }
}
