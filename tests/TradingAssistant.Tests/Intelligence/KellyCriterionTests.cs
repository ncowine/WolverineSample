using TradingAssistant.Application.Intelligence;

namespace TradingAssistant.Tests.Intelligence;

public class KellyCriterionTests
{
    // ── ComputeStats ──

    [Fact]
    public void ComputeStats_AllWinners()
    {
        var pnls = new List<decimal> { 100, 200, 150, 300, 250 };

        var stats = KellyCriterion.ComputeStats(pnls);

        Assert.Equal(5, stats.TotalTrades);
        Assert.Equal(5, stats.Winners);
        Assert.Equal(0, stats.Losers);
        Assert.Equal(1m, stats.WinRate);
        Assert.Equal(200m, stats.AvgWin);
        Assert.Equal(0m, stats.AvgLoss);
        Assert.Equal(0m, stats.PayoffRatio); // No losers → ratio undefined (0)
    }

    [Fact]
    public void ComputeStats_AllLosers()
    {
        var pnls = new List<decimal> { -100, -200, -150 };

        var stats = KellyCriterion.ComputeStats(pnls);

        Assert.Equal(3, stats.TotalTrades);
        Assert.Equal(0, stats.Winners);
        Assert.Equal(3, stats.Losers);
        Assert.Equal(0m, stats.WinRate);
        Assert.Equal(0m, stats.AvgWin);
        Assert.Equal(150m, stats.AvgLoss); // avg of abs values
    }

    [Fact]
    public void ComputeStats_MixedTrades()
    {
        // 3 wins, 2 losses → 60% win rate
        var pnls = new List<decimal> { 200, -100, 300, -100, 150 };

        var stats = KellyCriterion.ComputeStats(pnls);

        Assert.Equal(5, stats.TotalTrades);
        Assert.Equal(3, stats.Winners);
        Assert.Equal(2, stats.Losers);

        // Win rate: 3/5 = 0.60
        Assert.Equal(0.6m, stats.WinRate);

        // Avg win: (200 + 300 + 150) / 3 ≈ 216.67
        Assert.True(Math.Abs(stats.AvgWin - 216.67m) < 0.01m);

        // Avg loss: (100 + 100) / 2 = 100
        Assert.Equal(100m, stats.AvgLoss);

        // Payoff ratio: 216.67 / 100 = 2.167
        Assert.True(stats.PayoffRatio > 2.1m && stats.PayoffRatio < 2.2m);
    }

    [Fact]
    public void ComputeStats_EmptyList()
    {
        var stats = KellyCriterion.ComputeStats(new List<decimal>());

        Assert.Equal(0, stats.TotalTrades);
        Assert.Equal(0m, stats.WinRate);
    }

    [Fact]
    public void ComputeStats_WindowSize_TakesRecentTrades()
    {
        // 10 trades, window of 5 → should only use last 5
        var pnls = new List<decimal>
        {
            -100, -100, -100, -100, -100, // old losers (outside window)
            200, 200, 200, 200, 200       // recent winners (inside window)
        };

        var stats = KellyCriterion.ComputeStats(pnls, windowSize: 5);

        Assert.Equal(5, stats.TotalTrades);
        Assert.Equal(5, stats.Winners);
        Assert.Equal(1m, stats.WinRate);
    }

    [Fact]
    public void ComputeStats_ZeroPnl_CountsAsLoser()
    {
        var pnls = new List<decimal> { 0, 100 };

        var stats = KellyCriterion.ComputeStats(pnls);

        Assert.Equal(1, stats.Winners);
        Assert.Equal(1, stats.Losers); // Zero PnL = loser
    }

    // ── CalculateKellyFraction ──

    [Fact]
    public void KellyFraction_ClassicExample()
    {
        // Win rate 60%, payoff ratio 2:1
        // f* = (0.6 * 2 - 0.4) / 2 = (1.2 - 0.4) / 2 = 0.4
        var kelly = KellyCriterion.CalculateKellyFraction(0.6m, 2m);

        Assert.Equal(0.4m, kelly);
    }

    [Fact]
    public void KellyFraction_BreakevenSystem()
    {
        // Win rate 50%, payoff ratio 1:1 → f* = (0.5 * 1 - 0.5) / 1 = 0
        var kelly = KellyCriterion.CalculateKellyFraction(0.5m, 1m);

        Assert.Equal(0m, kelly);
    }

    [Fact]
    public void KellyFraction_NegativeEdge_ReturnsZero()
    {
        // Win rate 30%, payoff ratio 1:1 → f* = (0.3 - 0.7) / 1 = -0.4 → 0
        var kelly = KellyCriterion.CalculateKellyFraction(0.3m, 1m);

        Assert.Equal(0m, kelly);
    }

    [Fact]
    public void KellyFraction_HighWinRate_SmallPayoff()
    {
        // Win rate 80%, payoff ratio 0.5:1
        // f* = (0.8 * 0.5 - 0.2) / 0.5 = (0.4 - 0.2) / 0.5 = 0.4
        var kelly = KellyCriterion.CalculateKellyFraction(0.8m, 0.5m);

        Assert.Equal(0.4m, kelly);
    }

    [Fact]
    public void KellyFraction_LowWinRate_HighPayoff()
    {
        // Win rate 25%, payoff ratio 5:1
        // f* = (0.25 * 5 - 0.75) / 5 = (1.25 - 0.75) / 5 = 0.1
        var kelly = KellyCriterion.CalculateKellyFraction(0.25m, 5m);

        Assert.Equal(0.1m, kelly);
    }

    [Fact]
    public void KellyFraction_ZeroPayoff_ReturnsZero()
    {
        var kelly = KellyCriterion.CalculateKellyFraction(0.6m, 0m);
        Assert.Equal(0m, kelly);
    }

    [Fact]
    public void KellyFraction_ZeroWinRate_ReturnsZero()
    {
        var kelly = KellyCriterion.CalculateKellyFraction(0m, 2m);
        Assert.Equal(0m, kelly);
    }

    [Fact]
    public void KellyFraction_PerfectWinRate_ReturnsZero()
    {
        // Win rate = 1.0 is edge case: f* = (1*R - 0) / R = 1, but we guard >= 1
        var kelly = KellyCriterion.CalculateKellyFraction(1m, 2m);
        Assert.Equal(0m, kelly);
    }

    // ── CalculatePositionRisk ──

    [Fact]
    public void PositionRisk_NotEnoughTrades_FallsBackToFixed()
    {
        var pnls = Enumerable.Range(0, 20).Select(i => (decimal)(i % 2 == 0 ? 100 : -50)).ToList();

        var result = KellyCriterion.CalculatePositionRisk(
            pnls, equity: 100_000m, currentHeatPercent: 0m, maxPortfolioHeat: 6m);

        Assert.Equal("FixedFallback", result.Method);
        Assert.Equal(1m, result.RiskPercent);
        Assert.Equal(20, result.TradesUsed);
    }

    [Fact]
    public void PositionRisk_ExactlyMinTrades_UsesKelly()
    {
        // 30 trades: 18 winners (+200), 12 losers (-100) → 60% win, 2:1 payoff
        var pnls = new List<decimal>();
        for (int i = 0; i < 18; i++) pnls.Add(200m);
        for (int i = 0; i < 12; i++) pnls.Add(-100m);

        var result = KellyCriterion.CalculatePositionRisk(
            pnls, equity: 100_000m, currentHeatPercent: 0m, maxPortfolioHeat: 6m,
            fixedRiskPercent: 2m);

        Assert.Equal("Kelly", result.Method);
        // Raw Kelly = 0.4, half-Kelly = 0.2, as percent = 20% → capped by fixedRiskPercent (2%)
        Assert.Equal(0.4m, result.KellyFraction);
        Assert.Equal(0.2m, result.AdjustedFraction);
        Assert.Equal(2m, result.RiskPercent); // capped
    }

    [Fact]
    public void PositionRisk_Kelly_ConstrainedByHeatBudget()
    {
        // Good edge: 60% win, 2:1 payoff → Kelly 40%, half-Kelly 20%
        var pnls = new List<decimal>();
        for (int i = 0; i < 18; i++) pnls.Add(200m);
        for (int i = 0; i < 12; i++) pnls.Add(-100m);

        // Only 0.5% heat budget remaining
        var result = KellyCriterion.CalculatePositionRisk(
            pnls, equity: 100_000m, currentHeatPercent: 5.5m, maxPortfolioHeat: 6m,
            fixedRiskPercent: 2m);

        Assert.Equal("Kelly", result.Method);
        Assert.Equal(0.5m, result.RiskPercent); // constrained by remaining heat
    }

    [Fact]
    public void PositionRisk_NoEdge_FallsBackToFixed()
    {
        // 50% win, 1:1 payoff → Kelly = 0 → no edge
        var pnls = new List<decimal>();
        for (int i = 0; i < 15; i++) pnls.Add(100m);
        for (int i = 0; i < 15; i++) pnls.Add(-100m);

        var result = KellyCriterion.CalculatePositionRisk(
            pnls, equity: 100_000m, currentHeatPercent: 0m, maxPortfolioHeat: 6m);

        Assert.Equal("FixedFallback_NoEdge", result.Method);
        Assert.Equal(1m, result.RiskPercent); // falls back to default
    }

    [Fact]
    public void PositionRisk_HeatFullyConsumed_ReturnsZero()
    {
        var pnls = new List<decimal>();
        for (int i = 0; i < 18; i++) pnls.Add(200m);
        for (int i = 0; i < 12; i++) pnls.Add(-100m);

        var result = KellyCriterion.CalculatePositionRisk(
            pnls, equity: 100_000m, currentHeatPercent: 6m, maxPortfolioHeat: 6m,
            fixedRiskPercent: 2m);

        Assert.Equal(0m, result.RiskPercent);
    }

    [Fact]
    public void PositionRisk_EmptyTradeHistory_FallsBack()
    {
        var result = KellyCriterion.CalculatePositionRisk(
            new List<decimal>(), equity: 100_000m, currentHeatPercent: 0m, maxPortfolioHeat: 6m);

        Assert.Equal("FixedFallback", result.Method);
        Assert.Equal(1m, result.RiskPercent);
        Assert.Equal(0, result.TradesUsed);
    }

    [Fact]
    public void PositionRisk_CustomKellyMultiplier_QuarterKelly()
    {
        var pnls = new List<decimal>();
        for (int i = 0; i < 18; i++) pnls.Add(200m);
        for (int i = 0; i < 12; i++) pnls.Add(-100m);

        var result = KellyCriterion.CalculatePositionRisk(
            pnls, equity: 100_000m, currentHeatPercent: 0m, maxPortfolioHeat: 6m,
            fixedRiskPercent: 5m, kellyMultiplier: 0.25m);

        Assert.Equal("Kelly", result.Method);
        Assert.Equal(0.4m, result.KellyFraction);
        Assert.Equal(0.1m, result.AdjustedFraction); // quarter-Kelly
        // As percent = 10%, capped by fixedRiskPercent (5%)
        Assert.Equal(5m, result.RiskPercent);
    }

    [Fact]
    public void PositionRisk_MaxRiskCap_ClampsHighKelly()
    {
        // Very high edge system → large Kelly fraction
        var pnls = new List<decimal>();
        for (int i = 0; i < 27; i++) pnls.Add(500m); // 90% win
        for (int i = 0; i < 3; i++) pnls.Add(-100m);

        var result = KellyCriterion.CalculatePositionRisk(
            pnls, equity: 100_000m, currentHeatPercent: 0m, maxPortfolioHeat: 10m,
            fixedRiskPercent: 10m, maxRiskPercent: 5m);

        // Even though Kelly suggests more, max risk cap at 5%
        Assert.True(result.RiskPercent <= 5m);
    }

    // ── Integration: Known win rates and payoff ratios ──

    [Fact]
    public void KnownSystem_TrendFollowing()
    {
        // Typical trend following: 35% win rate, 3:1 payoff
        // Kelly = (0.35 * 3 - 0.65) / 3 = (1.05 - 0.65) / 3 = 0.1333
        var kelly = KellyCriterion.CalculateKellyFraction(0.35m, 3m);

        Assert.True(kelly > 0.13m && kelly < 0.14m);

        // Half-Kelly ≈ 6.67% risk
        var halfKelly = kelly * 0.5m;
        Assert.True(halfKelly > 0.06m && halfKelly < 0.07m);
    }

    [Fact]
    public void KnownSystem_MeanReversion()
    {
        // Typical mean reversion: 65% win rate, 1.2:1 payoff
        // Kelly = (0.65 * 1.2 - 0.35) / 1.2 = (0.78 - 0.35) / 1.2 = 0.3583
        var kelly = KellyCriterion.CalculateKellyFraction(0.65m, 1.2m);

        Assert.True(kelly > 0.35m && kelly < 0.36m);
    }

    [Fact]
    public void KnownSystem_Scalping()
    {
        // High win rate scalping: 75% win rate, 0.6:1 payoff
        // Kelly = (0.75 * 0.6 - 0.25) / 0.6 = (0.45 - 0.25) / 0.6 = 0.3333
        var kelly = KellyCriterion.CalculateKellyFraction(0.75m, 0.6m);

        Assert.True(kelly > 0.33m && kelly < 0.34m);
    }
}
