using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using TradingAssistant.Application.Handlers.Intelligence;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Domain.Intelligence;
using TradingAssistant.Domain.Intelligence.Enums;
using TradingAssistant.Domain.Trading;
using TradingAssistant.Infrastructure.Persistence;
using TradingAssistant.Tests.Helpers;
using static TradingAssistant.Application.Handlers.Intelligence.UpdateTournamentMetricsHandler;

namespace TradingAssistant.Tests.Intelligence;

public class TournamentLeaderboardTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // ── ComputeSharpe ───────────────────────────────────────────────

    [Fact]
    public void ComputeSharpe_LessThanTwoPoints_ReturnsZero()
    {
        var curve = new List<EquityPoint> { new("2026-03-01", 100_000m) };
        Assert.Equal(0m, ComputeSharpe(curve));
    }

    [Fact]
    public void ComputeSharpe_ConstantValue_ReturnsZero()
    {
        var curve = new List<EquityPoint>
        {
            new("2026-03-01", 100_000m),
            new("2026-03-02", 100_000m),
            new("2026-03-03", 100_000m),
        };
        Assert.Equal(0m, ComputeSharpe(curve));
    }

    [Fact]
    public void ComputeSharpe_PositiveReturns_ReturnsPositive()
    {
        // Rising equity with some variation
        var rng = new Random(42);
        var curve = new List<EquityPoint>();
        var value = 100_000m;
        for (int i = 0; i < 30; i++)
        {
            curve.Add(new EquityPoint($"2026-03-{i + 1:D2}", value));
            var dailyReturn = 0.005m + (decimal)(rng.NextDouble() * 0.01); // 0.5% to 1.5%
            value *= (1m + dailyReturn);
        }

        var sharpe = ComputeSharpe(curve);
        Assert.True(sharpe > 0, $"Expected positive Sharpe, got {sharpe}");
    }

    [Fact]
    public void ComputeSharpe_NegativeReturns_ReturnsNegative()
    {
        // Falling equity with some variation
        var rng = new Random(42);
        var curve = new List<EquityPoint>();
        var value = 100_000m;
        for (int i = 0; i < 30; i++)
        {
            curve.Add(new EquityPoint($"2026-03-{i + 1:D2}", value));
            var dailyReturn = -0.005m - (decimal)(rng.NextDouble() * 0.01); // -0.5% to -1.5%
            value *= (1m + dailyReturn);
        }

        var sharpe = ComputeSharpe(curve);
        Assert.True(sharpe < 0, $"Expected negative Sharpe, got {sharpe}");
    }

    // ── ComputeMaxDrawdown ──────────────────────────────────────────

    [Fact]
    public void ComputeMaxDrawdown_LessThanTwoPoints_ReturnsZero()
    {
        var curve = new List<EquityPoint> { new("2026-03-01", 100_000m) };
        Assert.Equal(0m, ComputeMaxDrawdown(curve));
    }

    [Fact]
    public void ComputeMaxDrawdown_NoDrawdown_ReturnsZero()
    {
        var curve = new List<EquityPoint>
        {
            new("2026-03-01", 100_000m),
            new("2026-03-02", 101_000m),
            new("2026-03-03", 102_000m),
        };
        Assert.Equal(0m, ComputeMaxDrawdown(curve));
    }

    [Fact]
    public void ComputeMaxDrawdown_KnownDrawdown_ReturnsCorrect()
    {
        var curve = new List<EquityPoint>
        {
            new("2026-03-01", 100_000m),
            new("2026-03-02", 110_000m), // Peak
            new("2026-03-03", 99_000m),  // Drawdown: (110000-99000)/110000 = 10%
            new("2026-03-04", 105_000m), // Recovery
        };

        var dd = ComputeMaxDrawdown(curve);
        Assert.Equal(10m, dd, 1); // ~10%
    }

    [Fact]
    public void ComputeMaxDrawdown_MultipleDrawdowns_ReturnsLargest()
    {
        var curve = new List<EquityPoint>
        {
            new("2026-03-01", 100_000m),
            new("2026-03-02", 110_000m), // Peak 1
            new("2026-03-03", 104_500m), // DD1: ~5%
            new("2026-03-04", 120_000m), // Peak 2
            new("2026-03-05", 96_000m),  // DD2: 20%
            new("2026-03-06", 115_000m), // Recovery
        };

        var dd = ComputeMaxDrawdown(curve);
        Assert.Equal(20m, dd, 1); // 20%
    }

    // ── DeserializeEquityCurve ──────────────────────────────────────

    [Fact]
    public void DeserializeEquityCurve_EmptyJson_ReturnsEmpty()
    {
        Assert.Empty(DeserializeEquityCurve("[]"));
        Assert.Empty(DeserializeEquityCurve(""));
        Assert.Empty(DeserializeEquityCurve(null!));
    }

    [Fact]
    public void DeserializeEquityCurve_ValidJson_ReturnsList()
    {
        var json = """[{"date":"2026-03-01","value":100000},{"date":"2026-03-02","value":101000}]""";
        var curve = DeserializeEquityCurve(json);

        Assert.Equal(2, curve.Count);
        Assert.Equal("2026-03-01", curve[0].Date);
        Assert.Equal(100_000m, curve[0].Value);
    }

    // ── UpdateTournamentMetricsHandler ──────────────────────────────

    [Fact]
    public async Task UpdateMetrics_ComputesReturnAndDays()
    {
        var dbName = Guid.NewGuid().ToString();
        using var tradingDb = TestDbContextFactory.Create(dbName + "_trading");
        using var intelligenceDb = TestIntelligenceDbContextFactory.Create(dbName + "_intel");
        var logger = NullLogger<UpdateTournamentMetricsHandler>.Instance;

        // Setup: paper account with portfolio
        var accountId = Guid.NewGuid();
        tradingDb.Accounts.Add(new Account
        {
            Id = accountId, UserId = Guid.Empty, Name = "Test",
            Balance = 100_000m, Currency = "USD", AccountType = AccountType.Paper
        });
        tradingDb.Portfolios.Add(new Portfolio
        {
            AccountId = accountId, TotalValue = 105_000m,
            CashBalance = 50_000m, InvestedValue = 55_000m, TotalPnL = 5_000m
        });
        await tradingDb.SaveChangesAsync();

        // Setup: tournament entry started 10 days ago
        var entry = new TournamentEntry
        {
            TournamentRunId = Guid.NewGuid(),
            StrategyId = Guid.NewGuid(),
            StrategyName = "Test Strategy",
            PaperAccountId = accountId,
            MarketCode = "US_SP500",
            StartDate = DateTime.UtcNow.AddDays(-10),
            Status = TournamentStatus.Active
        };
        intelligenceDb.TournamentEntries.Add(entry);
        await intelligenceDb.SaveChangesAsync();

        // Act
        await UpdateTournamentMetricsHandler.HandleAsync(
            new UpdateTournamentMetricsCommand(entry.Id),
            tradingDb, intelligenceDb, logger);

        // Assert
        var updated = await intelligenceDb.TournamentEntries.FindAsync(entry.Id);
        Assert.NotNull(updated);
        Assert.Equal(10, updated!.DaysActive);
        Assert.Equal(5m, updated.TotalReturn); // (105000-100000)/100000 * 100 = 5%

        // Equity curve should have one point
        var curve = DeserializeEquityCurve(updated.EquityCurveJson);
        Assert.Single(curve);
        Assert.Equal(105_000m, curve[0].Value);
    }

    [Fact]
    public async Task UpdateMetrics_EntryNotFound_DoesNotThrow()
    {
        var dbName = Guid.NewGuid().ToString();
        using var tradingDb = TestDbContextFactory.Create(dbName + "_trading");
        using var intelligenceDb = TestIntelligenceDbContextFactory.Create(dbName + "_intel");
        var logger = NullLogger<UpdateTournamentMetricsHandler>.Instance;

        // Should not throw
        await UpdateTournamentMetricsHandler.HandleAsync(
            new UpdateTournamentMetricsCommand(Guid.NewGuid()),
            tradingDb, intelligenceDb, logger);
    }

    // ── GetLeaderboardHandler ───────────────────────────────────────

    [Fact]
    public async Task GetLeaderboard_SortedBySharpe()
    {
        using var db = TestIntelligenceDbContextFactory.Create();

        db.TournamentEntries.AddRange(
            CreateEntry("US_SP500", "Strategy A", sharpe: 1.5m, days: 35),
            CreateEntry("US_SP500", "Strategy B", sharpe: 2.1m, days: 40),
            CreateEntry("US_SP500", "Strategy C", sharpe: 0.8m, days: 10),
            CreateEntry("IN_NIFTY50", "Strategy D", sharpe: 3.0m, days: 50) // different market
        );
        await db.SaveChangesAsync();

        var result = await GetLeaderboardHandler.HandleAsync(
            new GetLeaderboardQuery("US_SP500"), db);

        Assert.Equal(3, result.Count);
        Assert.Equal("Strategy B", result[0].StrategyName);
        Assert.Equal(2.1m, result[0].SharpeRatio);
        Assert.Equal("Strategy A", result[1].StrategyName);
        Assert.Equal("Strategy C", result[2].StrategyName);
    }

    [Fact]
    public async Task GetLeaderboard_PromotionEligibility_30Days()
    {
        using var db = TestIntelligenceDbContextFactory.Create();

        db.TournamentEntries.AddRange(
            CreateEntry("US_SP500", "Young", sharpe: 2.0m, days: 29),
            CreateEntry("US_SP500", "Mature", sharpe: 1.5m, days: 30),
            CreateEntry("US_SP500", "Old", sharpe: 1.0m, days: 60)
        );
        await db.SaveChangesAsync();

        var result = await GetLeaderboardHandler.HandleAsync(
            new GetLeaderboardQuery("US_SP500"), db);

        Assert.False(result[0].EligibleForPromotion); // 29 days
        Assert.True(result[1].EligibleForPromotion);  // 30 days
        Assert.True(result[2].EligibleForPromotion);  // 60 days
    }

    [Fact]
    public async Task GetLeaderboard_EmptyMarket_ReturnsEmpty()
    {
        using var db = TestIntelligenceDbContextFactory.Create();

        var result = await GetLeaderboardHandler.HandleAsync(
            new GetLeaderboardQuery("UNKNOWN"), db);

        Assert.Empty(result);
    }

    // ── GetTournamentEntryDetailHandler ─────────────────────────────

    [Fact]
    public async Task GetEntryDetail_WithEquityCurve_ReturnsFullDetail()
    {
        using var db = TestIntelligenceDbContextFactory.Create();

        var equityCurve = new List<EquityPoint>
        {
            new("2026-03-01", 100_000m),
            new("2026-03-02", 101_500m),
            new("2026-03-03", 103_000m),
        };

        var entry = CreateEntry("US_SP500", "Detailed Strategy", sharpe: 1.8m, days: 35);
        entry.EquityCurveJson = JsonSerializer.Serialize(equityCurve, JsonOpts);
        db.TournamentEntries.Add(entry);
        await db.SaveChangesAsync();

        var result = await GetTournamentEntryDetailHandler.HandleAsync(
            new GetTournamentEntryDetailQuery(entry.Id), db);

        Assert.NotNull(result);
        Assert.Equal("Detailed Strategy", result!.StrategyName);
        Assert.Equal(3, result.EquityCurve.Count);
        Assert.Equal("2026-03-01", result.EquityCurve[0].Date);
        Assert.Equal(100_000m, result.EquityCurve[0].Value);
        Assert.True(result.EligibleForPromotion);
    }

    [Fact]
    public async Task GetEntryDetail_NotFound_ReturnsNull()
    {
        using var db = TestIntelligenceDbContextFactory.Create();

        var result = await GetTournamentEntryDetailHandler.HandleAsync(
            new GetTournamentEntryDetailQuery(Guid.NewGuid()), db);

        Assert.Null(result);
    }

    // ── GetActiveStrategiesHandler ──────────────────────────────────

    [Fact]
    public async Task GetActiveStrategies_ReturnsOnlyPromoted()
    {
        using var db = TestIntelligenceDbContextFactory.Create();

        var active = CreateEntry("US_SP500", "Active One", sharpe: 1.5m, days: 40);
        active.Status = TournamentStatus.Active;

        var promoted1 = CreateEntry("US_SP500", "Promoted One", sharpe: 2.0m, days: 60);
        promoted1.Status = TournamentStatus.Promoted;
        promoted1.PromotedAt = DateTime.UtcNow.AddDays(-10);
        promoted1.AllocationPercent = 50m;

        var promoted2 = CreateEntry("US_SP500", "Promoted Two", sharpe: 1.8m, days: 45);
        promoted2.Status = TournamentStatus.Promoted;
        promoted2.PromotedAt = DateTime.UtcNow.AddDays(-5);

        var retired = CreateEntry("US_SP500", "Retired", sharpe: 0.5m, days: 90);
        retired.Status = TournamentStatus.Retired;

        db.TournamentEntries.AddRange(active, promoted1, promoted2, retired);
        await db.SaveChangesAsync();

        var result = await GetActiveStrategiesHandler.HandleAsync(
            new GetActiveStrategiesQuery("US_SP500"), db);

        Assert.Equal(2, result.Count);
        Assert.Equal("Promoted One", result[0].StrategyName);
        Assert.Equal(50m, result[0].AllocationPercent);
        Assert.Equal("Promoted Two", result[1].StrategyName);
    }

    [Fact]
    public async Task GetActiveStrategies_NoPromoted_ReturnsEmpty()
    {
        using var db = TestIntelligenceDbContextFactory.Create();

        db.TournamentEntries.Add(
            CreateEntry("US_SP500", "Active Only", sharpe: 1.0m, days: 40));
        await db.SaveChangesAsync();

        var result = await GetActiveStrategiesHandler.HandleAsync(
            new GetActiveStrategiesQuery("US_SP500"), db);

        Assert.Empty(result);
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private static TournamentEntry CreateEntry(
        string market, string name, decimal sharpe, int days,
        decimal totalReturn = 5m, decimal winRate = 0.55m)
    {
        return new TournamentEntry
        {
            TournamentRunId = Guid.NewGuid(),
            StrategyId = Guid.NewGuid(),
            StrategyName = name,
            PaperAccountId = Guid.NewGuid(),
            MarketCode = market,
            StartDate = DateTime.UtcNow.AddDays(-days),
            DaysActive = days,
            TotalTrades = 50,
            WinRate = winRate,
            SharpeRatio = sharpe,
            MaxDrawdown = 5m,
            TotalReturn = totalReturn,
            Status = TournamentStatus.Active,
            AllocationPercent = 25m
        };
    }
}
