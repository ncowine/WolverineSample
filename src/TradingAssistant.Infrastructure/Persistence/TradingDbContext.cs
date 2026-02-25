using Microsoft.EntityFrameworkCore;
using TradingAssistant.Domain.Audit;
using TradingAssistant.Domain.Identity;
using TradingAssistant.Domain.Trading;

namespace TradingAssistant.Infrastructure.Persistence;

public class TradingDbContext : DbContext
{
    public TradingDbContext(DbContextOptions<TradingDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<Portfolio> Portfolios => Set<Portfolio>();
    public DbSet<TradeExecution> TradeExecutions => Set<TradeExecution>();
    public DbSet<TradeNote> TradeNotes => Set<TradeNote>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Email).HasMaxLength(256).IsRequired();
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.Role).HasMaxLength(50).IsRequired();
        });

        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Balance).HasPrecision(18, 2);
            entity.Property(e => e.Currency).HasMaxLength(3);
            entity.HasOne(e => e.User)
                .WithMany(u => u.Accounts)
                .HasForeignKey(e => e.UserId);
            entity.HasIndex(e => e.UserId);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Symbol).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Quantity).HasPrecision(18, 4);
            entity.Property(e => e.Price).HasPrecision(18, 4);
            entity.HasOne(e => e.Account)
                .WithMany(a => a.Orders)
                .HasForeignKey(e => e.AccountId);
            entity.HasIndex(e => new { e.AccountId, e.Status });
        });

        modelBuilder.Entity<Position>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Symbol).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Quantity).HasPrecision(18, 4);
            entity.Property(e => e.AverageEntryPrice).HasPrecision(18, 4);
            entity.Property(e => e.CurrentPrice).HasPrecision(18, 4);
            entity.Ignore(e => e.UnrealizedPnL); // Computed property
            entity.HasOne(e => e.Account)
                .WithMany(a => a.Positions)
                .HasForeignKey(e => e.AccountId);
            entity.HasIndex(e => new { e.AccountId, e.Status });
        });

        modelBuilder.Entity<Portfolio>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TotalValue).HasPrecision(18, 2);
            entity.Property(e => e.CashBalance).HasPrecision(18, 2);
            entity.Property(e => e.InvestedValue).HasPrecision(18, 2);
            entity.Property(e => e.TotalPnL).HasPrecision(18, 2);
            entity.HasOne(e => e.Account)
                .WithOne(a => a.Portfolio)
                .HasForeignKey<Portfolio>(e => e.AccountId);
        });

        modelBuilder.Entity<TradeExecution>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Symbol).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Quantity).HasPrecision(18, 4);
            entity.Property(e => e.Price).HasPrecision(18, 4);
            entity.Property(e => e.Fee).HasPrecision(18, 4);
            entity.HasOne(e => e.Order)
                .WithMany(o => o.TradeExecutions)
                .HasForeignKey(e => e.OrderId);
        });

        modelBuilder.Entity<TradeNote>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Content).HasMaxLength(2000).IsRequired();
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.OrderId);
            entity.HasIndex(e => e.PositionId);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EntityType).HasMaxLength(100).IsRequired();
            entity.Property(e => e.EntityId).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Action).HasMaxLength(20).IsRequired();
            entity.HasIndex(e => e.EntityType);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.UserId);
        });
    }
}
