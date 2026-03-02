using Microsoft.EntityFrameworkCore;
using TradingAssistant.Application.Intelligence;
using TradingAssistant.Domain.Intelligence;
using TradingAssistant.Domain.Intelligence.Enums;
using TradingAssistant.Tests.Helpers;

namespace TradingAssistant.Tests.Intelligence;

public class GradualCapitalDeploymentTests
{
    private static readonly DateTime PromotionDate = new(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

    // ── CreateInitial ──

    [Fact]
    public void CreateInitial_StartsAt25Percent()
    {
        var state = GradualCapitalDeployment.CreateInitial(PromotionDate);

        Assert.Equal(DeploymentPhase.Initial25, state.Phase);
        Assert.Equal(25m, state.AllocationPercent);
        Assert.Equal(0, state.ProfitableTradeCount);
        Assert.Equal(PromotionDate, state.PromotionDate);
    }

    // ── Initial25 → Ramp50 ──

    [Fact]
    public void Initial25_NotEnoughTrades_Holds()
    {
        var eval = GradualCapitalDeployment.Evaluate(
            DeploymentPhase.Initial25,
            profitableTradeCount: 9,  // Need 10
            currentDate: PromotionDate.AddDays(14),
            promotionDate: PromotionDate,
            currentEquity: 10000m,
            peakEquity: 10000m);

        Assert.Equal(DeploymentPhase.Initial25, eval.NewPhase);
        Assert.Equal(25m, eval.NewAllocationPercent);
        Assert.False(eval.PhaseChanged);
        Assert.Contains("9/10 trades", eval.Detail);
    }

    [Fact]
    public void Initial25_NotEnoughTime_Holds()
    {
        var eval = GradualCapitalDeployment.Evaluate(
            DeploymentPhase.Initial25,
            profitableTradeCount: 15,  // Enough trades
            currentDate: PromotionDate.AddDays(13),  // Need 14
            promotionDate: PromotionDate,
            currentEquity: 10000m,
            peakEquity: 10000m);

        Assert.Equal(DeploymentPhase.Initial25, eval.NewPhase);
        Assert.False(eval.PhaseChanged);
        Assert.Contains("13/14 days", eval.Detail);
    }

    [Fact]
    public void Initial25_BothConditionsMet_AdvancesToRamp50()
    {
        var eval = GradualCapitalDeployment.Evaluate(
            DeploymentPhase.Initial25,
            profitableTradeCount: 10,
            currentDate: PromotionDate.AddDays(14),
            promotionDate: PromotionDate,
            currentEquity: 10000m,
            peakEquity: 10000m);

        Assert.Equal(DeploymentPhase.Ramp50, eval.NewPhase);
        Assert.Equal(50m, eval.NewAllocationPercent);
        Assert.True(eval.PhaseChanged);
        Assert.Contains("ADVANCED to 50%", eval.Detail);
    }

    [Fact]
    public void Initial25_ExcessTradesAndTime_AdvancesToRamp50()
    {
        var eval = GradualCapitalDeployment.Evaluate(
            DeploymentPhase.Initial25,
            profitableTradeCount: 20,  // More than needed
            currentDate: PromotionDate.AddDays(21),  // More than needed
            promotionDate: PromotionDate,
            currentEquity: 10000m,
            peakEquity: 10000m);

        Assert.Equal(DeploymentPhase.Ramp50, eval.NewPhase);
        Assert.True(eval.PhaseChanged);
    }

    [Fact]
    public void Initial25_SkipToFull100_WhenBothFull100ConditionsMet()
    {
        var eval = GradualCapitalDeployment.Evaluate(
            DeploymentPhase.Initial25,
            profitableTradeCount: 25,
            currentDate: PromotionDate.AddDays(28),
            promotionDate: PromotionDate,
            currentEquity: 10000m,
            peakEquity: 10000m);

        Assert.Equal(DeploymentPhase.Full100, eval.NewPhase);
        Assert.Equal(100m, eval.NewAllocationPercent);
        Assert.True(eval.PhaseChanged);
        Assert.Contains("ADVANCED to 100%", eval.Detail);
    }

    // ── Ramp50 → Full100 ──

    [Fact]
    public void Ramp50_NotEnoughTrades_Holds()
    {
        var eval = GradualCapitalDeployment.Evaluate(
            DeploymentPhase.Ramp50,
            profitableTradeCount: 24,  // Need 25
            currentDate: PromotionDate.AddDays(30),
            promotionDate: PromotionDate,
            currentEquity: 10000m,
            peakEquity: 10000m);

        Assert.Equal(DeploymentPhase.Ramp50, eval.NewPhase);
        Assert.Equal(50m, eval.NewAllocationPercent);
        Assert.False(eval.PhaseChanged);
    }

    [Fact]
    public void Ramp50_NotEnoughTime_Holds()
    {
        var eval = GradualCapitalDeployment.Evaluate(
            DeploymentPhase.Ramp50,
            profitableTradeCount: 30,
            currentDate: PromotionDate.AddDays(27),  // Need 28
            promotionDate: PromotionDate,
            currentEquity: 10000m,
            peakEquity: 10000m);

        Assert.Equal(DeploymentPhase.Ramp50, eval.NewPhase);
        Assert.False(eval.PhaseChanged);
    }

    [Fact]
    public void Ramp50_BothConditionsMet_AdvancesToFull100()
    {
        var eval = GradualCapitalDeployment.Evaluate(
            DeploymentPhase.Ramp50,
            profitableTradeCount: 25,
            currentDate: PromotionDate.AddDays(28),
            promotionDate: PromotionDate,
            currentEquity: 10000m,
            peakEquity: 10000m);

        Assert.Equal(DeploymentPhase.Full100, eval.NewPhase);
        Assert.Equal(100m, eval.NewAllocationPercent);
        Assert.True(eval.PhaseChanged);
        Assert.Contains("ADVANCED to 100%", eval.Detail);
    }

    // ── Full100 stays ──

    [Fact]
    public void Full100_StaysFull()
    {
        var eval = GradualCapitalDeployment.Evaluate(
            DeploymentPhase.Full100,
            profitableTradeCount: 50,
            currentDate: PromotionDate.AddDays(60),
            promotionDate: PromotionDate,
            currentEquity: 8000m,
            peakEquity: 10000m);  // 20% drawdown — no scale-down at Full100

        Assert.Equal(DeploymentPhase.Full100, eval.NewPhase);
        Assert.Equal(100m, eval.NewAllocationPercent);
        Assert.False(eval.PhaseChanged);
        Assert.False(eval.DrawdownScaleDown);
    }

    // ── Drawdown Scale-Down ──

    [Fact]
    public void Initial25_DrawdownOver5Percent_StaysAt25()
    {
        // Already at 25%, drawdown triggers but phase doesn't change
        var eval = GradualCapitalDeployment.Evaluate(
            DeploymentPhase.Initial25,
            profitableTradeCount: 5,
            currentDate: PromotionDate.AddDays(7),
            promotionDate: PromotionDate,
            currentEquity: 9400m,
            peakEquity: 10000m);  // 6% drawdown

        Assert.Equal(DeploymentPhase.Initial25, eval.NewPhase);
        Assert.Equal(25m, eval.NewAllocationPercent);
        Assert.True(eval.DrawdownScaleDown);
        Assert.False(eval.PhaseChanged); // Already at Initial25
        Assert.Contains("SCALE-DOWN", eval.Detail);
    }

    [Fact]
    public void Ramp50_DrawdownOver5Percent_ScalesDownTo25()
    {
        var eval = GradualCapitalDeployment.Evaluate(
            DeploymentPhase.Ramp50,
            profitableTradeCount: 15,
            currentDate: PromotionDate.AddDays(20),
            promotionDate: PromotionDate,
            currentEquity: 9400m,
            peakEquity: 10000m);  // 6% drawdown

        Assert.Equal(DeploymentPhase.Initial25, eval.NewPhase);
        Assert.Equal(25m, eval.NewAllocationPercent);
        Assert.True(eval.PhaseChanged);
        Assert.True(eval.DrawdownScaleDown);
        Assert.Contains("SCALE-DOWN", eval.Detail);
        Assert.Contains("6.0%", eval.Detail);
    }

    [Fact]
    public void Full100_DrawdownOver5Percent_NoScaleDown()
    {
        // Full100 is immune to drawdown scale-down
        var eval = GradualCapitalDeployment.Evaluate(
            DeploymentPhase.Full100,
            profitableTradeCount: 30,
            currentDate: PromotionDate.AddDays(35),
            promotionDate: PromotionDate,
            currentEquity: 9000m,
            peakEquity: 10000m);  // 10% drawdown

        Assert.Equal(DeploymentPhase.Full100, eval.NewPhase);
        Assert.False(eval.DrawdownScaleDown);
    }

    [Fact]
    public void DrawdownExactly5Percent_NoScaleDown()
    {
        // Exactly at threshold — strict > comparison
        var eval = GradualCapitalDeployment.Evaluate(
            DeploymentPhase.Ramp50,
            profitableTradeCount: 15,
            currentDate: PromotionDate.AddDays(20),
            promotionDate: PromotionDate,
            currentEquity: 9500m,
            peakEquity: 10000m);  // Exactly 5%

        Assert.Equal(DeploymentPhase.Ramp50, eval.NewPhase);
        Assert.False(eval.DrawdownScaleDown);
    }

    [Fact]
    public void DrawdownJustOver5Percent_ScalesDown()
    {
        var eval = GradualCapitalDeployment.Evaluate(
            DeploymentPhase.Ramp50,
            profitableTradeCount: 15,
            currentDate: PromotionDate.AddDays(20),
            promotionDate: PromotionDate,
            currentEquity: 9499m,
            peakEquity: 10000m);  // 5.01%

        Assert.True(eval.DrawdownScaleDown);
        Assert.Equal(DeploymentPhase.Initial25, eval.NewPhase);
    }

    [Fact]
    public void DrawdownCheckPrioritizedOverAdvancement()
    {
        // Strategy has enough trades + time to advance, but drawdown blocks it
        var eval = GradualCapitalDeployment.Evaluate(
            DeploymentPhase.Ramp50,
            profitableTradeCount: 30,
            currentDate: PromotionDate.AddDays(35),
            promotionDate: PromotionDate,
            currentEquity: 9000m,
            peakEquity: 10000m);  // 10% drawdown — should scale down, not advance

        Assert.Equal(DeploymentPhase.Initial25, eval.NewPhase);
        Assert.True(eval.DrawdownScaleDown);
    }

    // ── Edge Cases ──

    [Fact]
    public void ZeroPeakEquity_NoDrawdownCheck()
    {
        var eval = GradualCapitalDeployment.Evaluate(
            DeploymentPhase.Initial25,
            profitableTradeCount: 5,
            currentDate: PromotionDate.AddDays(7),
            promotionDate: PromotionDate,
            currentEquity: 0m,
            peakEquity: 0m);

        Assert.False(eval.DrawdownScaleDown);
        Assert.Equal(DeploymentPhase.Initial25, eval.NewPhase);
    }

    [Fact]
    public void SameDateAsPromotion_ZeroDays()
    {
        var eval = GradualCapitalDeployment.Evaluate(
            DeploymentPhase.Initial25,
            profitableTradeCount: 15,
            currentDate: PromotionDate,
            promotionDate: PromotionDate,
            currentEquity: 10000m,
            peakEquity: 10000m);

        Assert.Equal(DeploymentPhase.Initial25, eval.NewPhase);
        Assert.False(eval.PhaseChanged);
        Assert.Contains("0/14 days", eval.Detail);
    }

    [Fact]
    public void ExactBoundary_14Days10Trades_Advances()
    {
        var eval = GradualCapitalDeployment.Evaluate(
            DeploymentPhase.Initial25,
            profitableTradeCount: 10,
            currentDate: PromotionDate.AddDays(14),
            promotionDate: PromotionDate,
            currentEquity: 10000m,
            peakEquity: 10000m);

        Assert.Equal(DeploymentPhase.Ramp50, eval.NewPhase);
        Assert.True(eval.PhaseChanged);
    }

    [Fact]
    public void ExactBoundary_28Days25Trades_Advances()
    {
        var eval = GradualCapitalDeployment.Evaluate(
            DeploymentPhase.Ramp50,
            profitableTradeCount: 25,
            currentDate: PromotionDate.AddDays(28),
            promotionDate: PromotionDate,
            currentEquity: 10000m,
            peakEquity: 10000m);

        Assert.Equal(DeploymentPhase.Full100, eval.NewPhase);
        Assert.True(eval.PhaseChanged);
    }

    // ── ApplyAllocation ──

    [Fact]
    public void ApplyAllocation_25Percent()
    {
        Assert.Equal(25, GradualCapitalDeployment.ApplyAllocation(100, 25m));
    }

    [Fact]
    public void ApplyAllocation_50Percent()
    {
        Assert.Equal(50, GradualCapitalDeployment.ApplyAllocation(100, 50m));
    }

    [Fact]
    public void ApplyAllocation_100Percent()
    {
        Assert.Equal(100, GradualCapitalDeployment.ApplyAllocation(100, 100m));
    }

    [Fact]
    public void ApplyAllocation_Over100Percent_ReturnsTarget()
    {
        Assert.Equal(100, GradualCapitalDeployment.ApplyAllocation(100, 150m));
    }

    [Fact]
    public void ApplyAllocation_RoundsDown()
    {
        // 33 * 25% = 8.25 → 8
        Assert.Equal(8, GradualCapitalDeployment.ApplyAllocation(33, 25m));
    }

    [Fact]
    public void ApplyAllocation_ZeroShares_ReturnsZero()
    {
        Assert.Equal(0, GradualCapitalDeployment.ApplyAllocation(0, 50m));
    }

    [Fact]
    public void ApplyAllocation_ZeroAllocation_ReturnsZero()
    {
        Assert.Equal(0, GradualCapitalDeployment.ApplyAllocation(100, 0m));
    }

    [Fact]
    public void ApplyAllocation_NegativeShares_ReturnsZero()
    {
        Assert.Equal(0, GradualCapitalDeployment.ApplyAllocation(-10, 50m));
    }

    // ── Full lifecycle ──

    [Fact]
    public void FullLifecycle_PromotionToFullDeployment()
    {
        var state = GradualCapitalDeployment.CreateInitial(PromotionDate);
        Assert.Equal(25m, state.AllocationPercent);

        // Day 7, 5 trades — hold at 25%
        var eval1 = GradualCapitalDeployment.Evaluate(
            state.Phase, 5, PromotionDate.AddDays(7),
            PromotionDate, 10200m, 10200m);
        Assert.Equal(DeploymentPhase.Initial25, eval1.NewPhase);

        // Day 14, 10 trades — advance to 50%
        var eval2 = GradualCapitalDeployment.Evaluate(
            eval1.NewPhase, 10, PromotionDate.AddDays(14),
            PromotionDate, 10500m, 10500m);
        Assert.Equal(DeploymentPhase.Ramp50, eval2.NewPhase);
        Assert.Equal(50m, eval2.NewAllocationPercent);

        // Day 20, drawdown 6% — scale back to 25%
        var eval3 = GradualCapitalDeployment.Evaluate(
            eval2.NewPhase, 15, PromotionDate.AddDays(20),
            PromotionDate, 9870m, 10500m);
        Assert.Equal(DeploymentPhase.Initial25, eval3.NewPhase);
        Assert.True(eval3.DrawdownScaleDown);

        // Day 25, recovered, 18 trades — back to 50%
        var eval4 = GradualCapitalDeployment.Evaluate(
            eval3.NewPhase, 18, PromotionDate.AddDays(25),
            PromotionDate, 10600m, 10600m);
        Assert.Equal(DeploymentPhase.Ramp50, eval4.NewPhase);

        // Day 28, 25 trades — advance to 100%
        var eval5 = GradualCapitalDeployment.Evaluate(
            eval4.NewPhase, 25, PromotionDate.AddDays(28),
            PromotionDate, 10800m, 10800m);
        Assert.Equal(DeploymentPhase.Full100, eval5.NewPhase);
        Assert.Equal(100m, eval5.NewAllocationPercent);

        // Day 35, big drawdown — no scale-down at Full100
        var eval6 = GradualCapitalDeployment.Evaluate(
            eval5.NewPhase, 30, PromotionDate.AddDays(35),
            PromotionDate, 9000m, 10800m);
        Assert.Equal(DeploymentPhase.Full100, eval6.NewPhase);
        Assert.False(eval6.DrawdownScaleDown);
    }

    // ── TournamentEntry persistence ──

    [Fact]
    public async Task TournamentEntry_CanPersistAllocationPercent()
    {
        await using var db = TestIntelligenceDbContextFactory.Create();

        var entry = new TournamentEntry
        {
            TournamentRunId = Guid.NewGuid(),
            StrategyId = Guid.NewGuid(),
            PaperAccountId = Guid.NewGuid(),
            MarketCode = "US_SP500",
            StartDate = PromotionDate,
            Status = TournamentStatus.Promoted,
            PromotedAt = PromotionDate,
            AllocationPercent = 25m,
            TotalTrades = 10,
            WinRate = 0.65m,
            SharpeRatio = 1.5m,
            MaxDrawdown = 3.2m,
            TotalReturn = 5.5m
        };

        db.TournamentEntries.Add(entry);
        await db.SaveChangesAsync();

        var loaded = await db.TournamentEntries.FirstAsync(e => e.MarketCode == "US_SP500");

        Assert.Equal(25m, loaded.AllocationPercent);
        Assert.Equal(TournamentStatus.Promoted, loaded.Status);
        Assert.Equal(PromotionDate, loaded.PromotedAt);
    }

    [Fact]
    public async Task TournamentEntry_UpdateAllocationDuringRamp()
    {
        await using var db = TestIntelligenceDbContextFactory.Create();

        var entry = new TournamentEntry
        {
            TournamentRunId = Guid.NewGuid(),
            StrategyId = Guid.NewGuid(),
            PaperAccountId = Guid.NewGuid(),
            MarketCode = "US_SP500",
            StartDate = PromotionDate,
            Status = TournamentStatus.Promoted,
            PromotedAt = PromotionDate,
            AllocationPercent = 25m
        };

        db.TournamentEntries.Add(entry);
        await db.SaveChangesAsync();

        // Simulate ramp-up
        var eval = GradualCapitalDeployment.Evaluate(
            DeploymentPhase.Initial25, 12,
            PromotionDate.AddDays(15), PromotionDate,
            10500m, 10500m);

        entry.AllocationPercent = eval.NewAllocationPercent;
        await db.SaveChangesAsync();

        var loaded = await db.TournamentEntries.FirstAsync(e => e.MarketCode == "US_SP500");
        Assert.Equal(50m, loaded.AllocationPercent);
    }

    [Fact]
    public async Task TournamentEntry_IndexOnStatusAndMarketCode()
    {
        await using var db = TestIntelligenceDbContextFactory.Create();

        var indexes = db.Model.FindEntityType(typeof(TournamentEntry))!.GetIndexes();
        Assert.Contains(indexes, i =>
            i.Properties.Any(p => p.Name == "Status") &&
            i.Properties.Any(p => p.Name == "MarketCode"));
        Assert.Contains(indexes, i =>
            i.Properties.Any(p => p.Name == "StrategyId"));
    }

    // ── Theory: various trade/day combos ──

    [Theory]
    [InlineData(0, 0, DeploymentPhase.Initial25)]
    [InlineData(9, 14, DeploymentPhase.Initial25)]    // Trades short
    [InlineData(10, 13, DeploymentPhase.Initial25)]   // Days short
    [InlineData(10, 14, DeploymentPhase.Ramp50)]      // Both met
    [InlineData(24, 28, DeploymentPhase.Ramp50)]      // Trades short for Full
    [InlineData(25, 27, DeploymentPhase.Ramp50)]      // Days short for Full
    [InlineData(25, 28, DeploymentPhase.Full100)]     // Both met — skip to Full from Initial
    public void Initial25_VariousCombinations(int trades, int days, DeploymentPhase expectedPhase)
    {
        var eval = GradualCapitalDeployment.Evaluate(
            DeploymentPhase.Initial25, trades,
            PromotionDate.AddDays(days), PromotionDate,
            10000m, 10000m);

        Assert.Equal(expectedPhase, eval.NewPhase);
    }
}
