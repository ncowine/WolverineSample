using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using TradingAssistant.Application.Handlers.Intelligence;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Domain.Intelligence;
using TradingAssistant.Domain.Intelligence.Enums;
using TradingAssistant.Tests.Helpers;

namespace TradingAssistant.Tests.Intelligence;

public class StrategyPromotionRetirementTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // ── PromoteStrategyHandler ──────────────────────────────────────

    [Fact]
    public async Task Promote_MeetsCriteria_Success()
    {
        using var db = TestIntelligenceDbContextFactory.Create();
        var logger = NullLogger<PromoteStrategyHandler>.Instance;

        var entry = CreateEntry(days: 35, sharpe: 1.5m, dd: 8m, winRate: 0.55m);
        db.TournamentEntries.Add(entry);
        await db.SaveChangesAsync();

        var result = await PromoteStrategyHandler.HandleAsync(
            new PromoteStrategyCommand(entry.Id), db, logger);

        Assert.True(result.Success);
        Assert.Equal(PromoteStrategyHandler.InitialAllocation, result.AllocationPercent);
        Assert.Equal(25m, result.AllocationPercent);

        var updated = await db.TournamentEntries.FindAsync(entry.Id);
        Assert.Equal(TournamentStatus.Promoted, updated!.Status);
        Assert.NotNull(updated.PromotedAt);
    }

    [Fact]
    public async Task Promote_InsufficientDays_Fails()
    {
        using var db = TestIntelligenceDbContextFactory.Create();
        var logger = NullLogger<PromoteStrategyHandler>.Instance;

        var entry = CreateEntry(days: 29, sharpe: 2.0m, dd: 5m, winRate: 0.60m);
        db.TournamentEntries.Add(entry);
        await db.SaveChangesAsync();

        var result = await PromoteStrategyHandler.HandleAsync(
            new PromoteStrategyCommand(entry.Id), db, logger);

        Assert.False(result.Success);
        Assert.Contains("Days active", result.Error!);
    }

    [Fact]
    public async Task Promote_LowSharpe_Fails()
    {
        using var db = TestIntelligenceDbContextFactory.Create();
        var logger = NullLogger<PromoteStrategyHandler>.Instance;

        var entry = CreateEntry(days: 35, sharpe: 0.9m, dd: 5m, winRate: 0.55m);
        db.TournamentEntries.Add(entry);
        await db.SaveChangesAsync();

        var result = await PromoteStrategyHandler.HandleAsync(
            new PromoteStrategyCommand(entry.Id), db, logger);

        Assert.False(result.Success);
        Assert.Contains("Sharpe", result.Error!);
    }

    [Fact]
    public async Task Promote_HighDrawdown_Fails()
    {
        using var db = TestIntelligenceDbContextFactory.Create();
        var logger = NullLogger<PromoteStrategyHandler>.Instance;

        var entry = CreateEntry(days: 35, sharpe: 1.5m, dd: 12m, winRate: 0.55m);
        db.TournamentEntries.Add(entry);
        await db.SaveChangesAsync();

        var result = await PromoteStrategyHandler.HandleAsync(
            new PromoteStrategyCommand(entry.Id), db, logger);

        Assert.False(result.Success);
        Assert.Contains("drawdown", result.Error!);
    }

    [Fact]
    public async Task Promote_LowWinRate_Fails()
    {
        using var db = TestIntelligenceDbContextFactory.Create();
        var logger = NullLogger<PromoteStrategyHandler>.Instance;

        var entry = CreateEntry(days: 35, sharpe: 1.5m, dd: 5m, winRate: 0.45m);
        db.TournamentEntries.Add(entry);
        await db.SaveChangesAsync();

        var result = await PromoteStrategyHandler.HandleAsync(
            new PromoteStrategyCommand(entry.Id), db, logger);

        Assert.False(result.Success);
        Assert.Contains("Win rate", result.Error!);
    }

    [Fact]
    public async Task Promote_ForceOverride_BypassesCriteria()
    {
        using var db = TestIntelligenceDbContextFactory.Create();
        var logger = NullLogger<PromoteStrategyHandler>.Instance;

        // Entry that fails all criteria
        var entry = CreateEntry(days: 5, sharpe: 0.2m, dd: 20m, winRate: 0.30m);
        db.TournamentEntries.Add(entry);
        await db.SaveChangesAsync();

        var result = await PromoteStrategyHandler.HandleAsync(
            new PromoteStrategyCommand(entry.Id, Force: true), db, logger);

        Assert.True(result.Success);
        Assert.Contains("Manual", result.Reason!);
    }

    [Fact]
    public async Task Promote_AlreadyPromoted_Fails()
    {
        using var db = TestIntelligenceDbContextFactory.Create();
        var logger = NullLogger<PromoteStrategyHandler>.Instance;

        var entry = CreateEntry(days: 35, sharpe: 1.5m, dd: 5m, winRate: 0.55m);
        entry.Status = TournamentStatus.Promoted;
        db.TournamentEntries.Add(entry);
        await db.SaveChangesAsync();

        var result = await PromoteStrategyHandler.HandleAsync(
            new PromoteStrategyCommand(entry.Id), db, logger);

        Assert.False(result.Success);
        Assert.Contains("already promoted", result.Error!);
    }

    [Fact]
    public async Task Promote_Retired_Fails()
    {
        using var db = TestIntelligenceDbContextFactory.Create();
        var logger = NullLogger<PromoteStrategyHandler>.Instance;

        var entry = CreateEntry(days: 35, sharpe: 1.5m, dd: 5m, winRate: 0.55m);
        entry.Status = TournamentStatus.Retired;
        db.TournamentEntries.Add(entry);
        await db.SaveChangesAsync();

        var result = await PromoteStrategyHandler.HandleAsync(
            new PromoteStrategyCommand(entry.Id), db, logger);

        Assert.False(result.Success);
        Assert.Contains("retired", result.Error!);
    }

    [Fact]
    public async Task Promote_NotFound_Fails()
    {
        using var db = TestIntelligenceDbContextFactory.Create();
        var logger = NullLogger<PromoteStrategyHandler>.Instance;

        var result = await PromoteStrategyHandler.HandleAsync(
            new PromoteStrategyCommand(Guid.NewGuid()), db, logger);

        Assert.False(result.Success);
        Assert.Contains("not found", result.Error!);
    }

    // ── Promotion Criteria Validation ───────────────────────────────

    [Fact]
    public void ValidatePromotionCriteria_AllPass_NoFailures()
    {
        var failures = PromoteStrategyHandler.ValidatePromotionCriteria(
            daysActive: 30, sharpeRatio: 1.0m, maxDrawdown: 10m, winRate: 0.50m);

        Assert.Empty(failures);
    }

    [Fact]
    public void ValidatePromotionCriteria_AllFail_FourFailures()
    {
        var failures = PromoteStrategyHandler.ValidatePromotionCriteria(
            daysActive: 5, sharpeRatio: 0.1m, maxDrawdown: 20m, winRate: 0.30m);

        Assert.Equal(4, failures.Count);
    }

    // ── RetireStrategyHandler ───────────────────────────────────────

    [Fact]
    public async Task Retire_LowSharpe_Success()
    {
        using var db = TestIntelligenceDbContextFactory.Create();
        var logger = NullLogger<RetireStrategyHandler>.Instance;

        var entry = CreateEntry(days: 65, sharpe: 0.2m, dd: 15m, winRate: 0.40m);
        db.TournamentEntries.Add(entry);
        await db.SaveChangesAsync();

        var result = await RetireStrategyHandler.HandleAsync(
            new RetireStrategyCommand(entry.Id), db, logger);

        Assert.True(result.Success);
        Assert.Contains("Sharpe", result.RetirementReason);

        var updated = await db.TournamentEntries.FindAsync(entry.Id);
        Assert.Equal(TournamentStatus.Retired, updated!.Status);
        Assert.NotNull(updated.RetiredAt);
        Assert.NotNull(updated.RetirementReason);
    }

    [Fact]
    public async Task Retire_ConsecutiveLosingMonths_Success()
    {
        using var db = TestIntelligenceDbContextFactory.Create();
        var logger = NullLogger<RetireStrategyHandler>.Instance;

        // Build equity curve with 3 consecutive losing months
        var curve = BuildLosingEquityCurve();
        var entry = CreateEntry(days: 120, sharpe: 0.5m, dd: 10m, winRate: 0.45m);
        entry.EquityCurveJson = JsonSerializer.Serialize(curve, JsonOpts);
        db.TournamentEntries.Add(entry);
        await db.SaveChangesAsync();

        var result = await RetireStrategyHandler.HandleAsync(
            new RetireStrategyCommand(entry.Id), db, logger);

        Assert.True(result.Success);
        Assert.Contains("consecutive losing months", result.RetirementReason);
    }

    [Fact]
    public async Task Retire_NotMeetingCriteria_Fails()
    {
        using var db = TestIntelligenceDbContextFactory.Create();
        var logger = NullLogger<RetireStrategyHandler>.Instance;

        // Good strategy that shouldn't be retired
        var entry = CreateEntry(days: 35, sharpe: 1.5m, dd: 5m, winRate: 0.55m);
        db.TournamentEntries.Add(entry);
        await db.SaveChangesAsync();

        var result = await RetireStrategyHandler.HandleAsync(
            new RetireStrategyCommand(entry.Id), db, logger);

        Assert.False(result.Success);
        Assert.Contains("does not meet", result.Error!);
    }

    [Fact]
    public async Task Retire_ForceOverride_Success()
    {
        using var db = TestIntelligenceDbContextFactory.Create();
        var logger = NullLogger<RetireStrategyHandler>.Instance;

        var entry = CreateEntry(days: 35, sharpe: 1.5m, dd: 5m, winRate: 0.55m);
        db.TournamentEntries.Add(entry);
        await db.SaveChangesAsync();

        var result = await RetireStrategyHandler.HandleAsync(
            new RetireStrategyCommand(entry.Id, Force: true), db, logger);

        Assert.True(result.Success);
        Assert.Equal("Manual retirement", result.RetirementReason);
    }

    [Fact]
    public async Task Retire_WithCustomReason_UsesProvidedReason()
    {
        using var db = TestIntelligenceDbContextFactory.Create();
        var logger = NullLogger<RetireStrategyHandler>.Instance;

        var entry = CreateEntry(days: 35, sharpe: 1.5m, dd: 5m, winRate: 0.55m);
        db.TournamentEntries.Add(entry);
        await db.SaveChangesAsync();

        var result = await RetireStrategyHandler.HandleAsync(
            new RetireStrategyCommand(entry.Id, Reason: "Strategy no longer fits market"), db, logger);

        Assert.True(result.Success);
        Assert.Equal("Strategy no longer fits market", result.RetirementReason);
    }

    [Fact]
    public async Task Retire_AlreadyRetired_Fails()
    {
        using var db = TestIntelligenceDbContextFactory.Create();
        var logger = NullLogger<RetireStrategyHandler>.Instance;

        var entry = CreateEntry(days: 35, sharpe: 0.1m, dd: 20m, winRate: 0.30m);
        entry.Status = TournamentStatus.Retired;
        db.TournamentEntries.Add(entry);
        await db.SaveChangesAsync();

        var result = await RetireStrategyHandler.HandleAsync(
            new RetireStrategyCommand(entry.Id), db, logger);

        Assert.False(result.Success);
        Assert.Contains("already retired", result.Error!);
    }

    [Fact]
    public async Task Retire_NotFound_Fails()
    {
        using var db = TestIntelligenceDbContextFactory.Create();
        var logger = NullLogger<RetireStrategyHandler>.Instance;

        var result = await RetireStrategyHandler.HandleAsync(
            new RetireStrategyCommand(Guid.NewGuid()), db, logger);

        Assert.False(result.Success);
        Assert.Contains("not found", result.Error!);
    }

    // ── CountConsecutiveLosingMonths ────────────────────────────────

    [Fact]
    public void CountLosingMonths_EmptyJson_ReturnsZero()
    {
        Assert.Equal(0, RetireStrategyHandler.CountConsecutiveLosingMonths("[]"));
        Assert.Equal(0, RetireStrategyHandler.CountConsecutiveLosingMonths(""));
    }

    [Fact]
    public void CountLosingMonths_ThreeLosingMonths_ReturnsThree()
    {
        var curve = BuildLosingEquityCurve();
        var json = JsonSerializer.Serialize(curve, JsonOpts);

        var count = RetireStrategyHandler.CountConsecutiveLosingMonths(json);
        Assert.Equal(3, count);
    }

    [Fact]
    public void CountLosingMonths_MixedMonths_CountsTrailing()
    {
        // Jan: +5%, Feb: -2%, Mar: +3% (only 1 trailing loss: none at end)
        var curve = new List<RetireStrategyHandler.EquityPoint>
        {
            new("2026-01-01", 100_000m),
            new("2026-01-31", 105_000m),
            new("2026-02-01", 105_000m),
            new("2026-02-28", 102_900m), // losing
            new("2026-03-01", 102_900m),
            new("2026-03-31", 106_000m), // winning
        };
        var json = JsonSerializer.Serialize(curve, JsonOpts);

        var count = RetireStrategyHandler.CountConsecutiveLosingMonths(json);
        Assert.Equal(0, count); // Last month is positive
    }

    // ── Archived Performance History ────────────────────────────────

    [Fact]
    public async Task Retire_PreservesFullPerformanceHistory()
    {
        using var db = TestIntelligenceDbContextFactory.Create();
        var logger = NullLogger<RetireStrategyHandler>.Instance;

        var entry = CreateEntry(days: 65, sharpe: 0.2m, dd: 15m, winRate: 0.40m);
        entry.TotalTrades = 120;
        entry.TotalReturn = -8.5m;
        entry.EquityCurveJson = """[{"date":"2026-01-01","value":100000}]""";
        db.TournamentEntries.Add(entry);
        await db.SaveChangesAsync();

        await RetireStrategyHandler.HandleAsync(
            new RetireStrategyCommand(entry.Id), db, logger);

        var retired = await db.TournamentEntries.FindAsync(entry.Id);
        Assert.NotNull(retired);
        Assert.Equal(TournamentStatus.Retired, retired!.Status);
        // Performance history preserved
        Assert.Equal(120, retired.TotalTrades);
        Assert.Equal(-8.5m, retired.TotalReturn);
        Assert.Equal(0.2m, retired.SharpeRatio);
        Assert.Contains("100000", retired.EquityCurveJson);
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private static TournamentEntry CreateEntry(
        int days, decimal sharpe, decimal dd, decimal winRate)
    {
        return new TournamentEntry
        {
            TournamentRunId = Guid.NewGuid(),
            StrategyId = Guid.NewGuid(),
            StrategyName = "Test Strategy",
            PaperAccountId = Guid.NewGuid(),
            MarketCode = "US_SP500",
            StartDate = DateTime.UtcNow.AddDays(-days),
            DaysActive = days,
            TotalTrades = 50,
            WinRate = winRate,
            SharpeRatio = sharpe,
            MaxDrawdown = dd,
            TotalReturn = 5m,
            Status = TournamentStatus.Active,
            AllocationPercent = 25m
        };
    }

    private static List<RetireStrategyHandler.EquityPoint> BuildLosingEquityCurve()
    {
        // 4 months: Jan winning, Feb/Mar/Apr losing
        return
        [
            new("2026-01-01", 100_000m),
            new("2026-01-31", 105_000m), // Jan: +5%
            new("2026-02-01", 105_000m),
            new("2026-02-28", 102_000m), // Feb: -2.86%
            new("2026-03-01", 102_000m),
            new("2026-03-31", 99_000m),  // Mar: -2.94%
            new("2026-04-01", 99_000m),
            new("2026-04-30", 96_000m),  // Apr: -3.03%
        ];
    }
}
