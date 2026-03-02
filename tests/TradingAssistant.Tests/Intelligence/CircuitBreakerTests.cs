using TradingAssistant.Application.Intelligence;

namespace TradingAssistant.Tests.Intelligence;

public class CircuitBreakerTests
{
    // ── CalculateDrawdown ──

    [Fact]
    public void CalculateDrawdown_Basic()
    {
        // Peak $100K, current $85K → 15% drawdown
        Assert.Equal(15m, CircuitBreaker.CalculateDrawdown(85_000m, 100_000m));
    }

    [Fact]
    public void CalculateDrawdown_NoPeak_ReturnsZero()
    {
        Assert.Equal(0m, CircuitBreaker.CalculateDrawdown(100m, 0m));
    }

    [Fact]
    public void CalculateDrawdown_AtPeak_ReturnsZero()
    {
        Assert.Equal(0m, CircuitBreaker.CalculateDrawdown(100_000m, 100_000m));
    }

    [Fact]
    public void CalculateDrawdown_AbovePeak_ReturnsZero()
    {
        Assert.Equal(0m, CircuitBreaker.CalculateDrawdown(110_000m, 100_000m));
    }

    // ── HasEquityRecovered ──

    [Fact]
    public void HasEquityRecovered_AtThreshold_True()
    {
        // Peak $100K, recovery 5% → threshold = $95K
        Assert.True(CircuitBreaker.HasEquityRecovered(95_000m, 100_000m, 5m));
    }

    [Fact]
    public void HasEquityRecovered_AboveThreshold_True()
    {
        Assert.True(CircuitBreaker.HasEquityRecovered(98_000m, 100_000m, 5m));
    }

    [Fact]
    public void HasEquityRecovered_BelowThreshold_False()
    {
        Assert.False(CircuitBreaker.HasEquityRecovered(90_000m, 100_000m, 5m));
    }

    // ── CancelPendingOrders ──

    [Fact]
    public void CancelPendingOrders_ClearsAndReturnsCount()
    {
        var orders = new List<string> { "order1", "order2", "order3" };
        var cancelled = CircuitBreaker.CancelPendingOrders(orders);

        Assert.Equal(3, cancelled);
        Assert.Empty(orders);
    }

    [Fact]
    public void CancelPendingOrders_EmptyList_ReturnsZero()
    {
        var orders = new List<int>();
        Assert.Equal(0, CircuitBreaker.CancelPendingOrders(orders));
    }

    // ── Evaluate: Activation ──

    [Fact]
    public void Evaluate_DrawdownExceedsThreshold_Activates()
    {
        var eval = CircuitBreaker.Evaluate(
            currentEquity: 85_000m,
            peakEquity: 100_000m,
            isCurrentlyActive: false,
            thresholdPercent: 15m);

        Assert.True(eval.ShouldActivate);
        Assert.False(eval.ShouldDeactivate);
        Assert.True(eval.IsActive);
        Assert.Equal(15m, eval.DrawdownPercent);
        Assert.Contains("ACTIVATED", eval.Detail);
    }

    [Fact]
    public void Evaluate_DrawdownBelowThreshold_NoActivation()
    {
        var eval = CircuitBreaker.Evaluate(
            currentEquity: 90_000m,
            peakEquity: 100_000m,
            isCurrentlyActive: false,
            thresholdPercent: 15m);

        Assert.False(eval.ShouldActivate);
        Assert.False(eval.IsActive);
        Assert.Equal(10m, eval.DrawdownPercent);
        Assert.Contains("OK", eval.Detail);
    }

    [Fact]
    public void Evaluate_ExactlyAtThreshold_Activates()
    {
        // >= threshold → activate
        var eval = CircuitBreaker.Evaluate(
            currentEquity: 85_000m,
            peakEquity: 100_000m,
            isCurrentlyActive: false,
            thresholdPercent: 15m);

        Assert.True(eval.ShouldActivate);
    }

    [Fact]
    public void Evaluate_Activation_IncludesRegimeContext()
    {
        var eval = CircuitBreaker.Evaluate(
            currentEquity: 80_000m,
            peakEquity: 100_000m,
            isCurrentlyActive: false,
            thresholdPercent: 15m,
            currentRegime: "Bear",
            regimeConfidence: 0.85m);

        Assert.True(eval.ShouldActivate);
        Assert.Equal("Bear", eval.Regime);
        Assert.Equal(0.85m, eval.RegimeConfidence);
        Assert.Contains("Bear", eval.Detail);
    }

    [Fact]
    public void Evaluate_CustomThreshold_5Percent()
    {
        var eval = CircuitBreaker.Evaluate(
            currentEquity: 94_000m,
            peakEquity: 100_000m,
            isCurrentlyActive: false,
            thresholdPercent: 5m);

        Assert.True(eval.ShouldActivate);
        Assert.Equal(6m, eval.DrawdownPercent);
    }

    // ── Evaluate: Deactivation ──

    [Fact]
    public void Evaluate_EquityRecovered_RegimeSafe_Deactivates()
    {
        var eval = CircuitBreaker.Evaluate(
            currentEquity: 96_000m,
            peakEquity: 100_000m,
            isCurrentlyActive: true,
            recoveryPercent: 5m,
            currentRegime: "Bull");

        Assert.False(eval.ShouldActivate);
        Assert.True(eval.ShouldDeactivate);
        Assert.False(eval.IsActive);
        Assert.Contains("DEACTIVATED", eval.Detail);
    }

    [Fact]
    public void Evaluate_EquityRecovered_HighVol_StaysActive()
    {
        // Equity recovered, but regime is HighVolatility → stay active
        var eval = CircuitBreaker.Evaluate(
            currentEquity: 96_000m,
            peakEquity: 100_000m,
            isCurrentlyActive: true,
            recoveryPercent: 5m,
            currentRegime: "HighVolatility");

        Assert.False(eval.ShouldDeactivate);
        Assert.True(eval.IsActive);
        Assert.Contains("HighVolatility", eval.Detail);
        Assert.Contains("waiting for regime change", eval.Detail);
    }

    [Fact]
    public void Evaluate_EquityNotRecovered_StaysActive()
    {
        var eval = CircuitBreaker.Evaluate(
            currentEquity: 90_000m,
            peakEquity: 100_000m,
            isCurrentlyActive: true,
            recoveryPercent: 5m,
            currentRegime: "Bull");

        Assert.False(eval.ShouldDeactivate);
        Assert.True(eval.IsActive);
        Assert.Contains("ACTIVE", eval.Detail);
        Assert.Contains("recovery threshold", eval.Detail);
    }

    [Fact]
    public void Evaluate_ExactlyAtRecoveryThreshold_Deactivates()
    {
        // Peak $100K, recovery 5% → threshold $95K, equity = $95K
        var eval = CircuitBreaker.Evaluate(
            currentEquity: 95_000m,
            peakEquity: 100_000m,
            isCurrentlyActive: true,
            recoveryPercent: 5m,
            currentRegime: "Sideways");

        Assert.True(eval.ShouldDeactivate);
    }

    [Fact]
    public void Evaluate_NoRegime_DeactivatesOnRecovery()
    {
        // No regime data → treat as safe (null != "HighVolatility")
        var eval = CircuitBreaker.Evaluate(
            currentEquity: 96_000m,
            peakEquity: 100_000m,
            isCurrentlyActive: true,
            recoveryPercent: 5m,
            currentRegime: null);

        Assert.True(eval.ShouldDeactivate);
    }

    // ── Evaluate: State machine flow ──

    [Fact]
    public void StateMachine_FullLifecycle()
    {
        // 1. Normal operation
        var eval1 = CircuitBreaker.Evaluate(100_000m, 100_000m, false, 15m);
        Assert.False(eval1.IsActive);
        Assert.Equal(0m, eval1.DrawdownPercent);

        // 2. Drawdown hits threshold → activate
        var eval2 = CircuitBreaker.Evaluate(84_000m, 100_000m, false, 15m);
        Assert.True(eval2.ShouldActivate);
        Assert.True(eval2.IsActive);

        // 3. Still in drawdown → stays active
        var eval3 = CircuitBreaker.Evaluate(88_000m, 100_000m, true, 15m, 5m, "Bear");
        Assert.True(eval3.IsActive);
        Assert.False(eval3.ShouldDeactivate);

        // 4. Equity recovers but regime is HighVol → stays active
        var eval4 = CircuitBreaker.Evaluate(96_000m, 100_000m, true, 15m, 5m, "HighVolatility");
        Assert.True(eval4.IsActive);
        Assert.False(eval4.ShouldDeactivate);

        // 5. Regime changes to Bull → deactivates
        var eval5 = CircuitBreaker.Evaluate(96_000m, 100_000m, true, 15m, 5m, "Bull");
        Assert.True(eval5.ShouldDeactivate);
        Assert.False(eval5.IsActive);
    }

    // ── Evaluate: Edge cases ──

    [Fact]
    public void Evaluate_ZeroPeakEquity_NoActivation()
    {
        var eval = CircuitBreaker.Evaluate(0m, 0m, false);
        Assert.False(eval.ShouldActivate);
        Assert.Equal(0m, eval.DrawdownPercent);
    }

    [Fact]
    public void Evaluate_100PercentDrawdown_Activates()
    {
        var eval = CircuitBreaker.Evaluate(0m, 100_000m, false, 15m);
        Assert.True(eval.ShouldActivate);
        Assert.Equal(100m, eval.DrawdownPercent);
    }

    [Fact]
    public void Evaluate_TightThreshold_ActivatesEarly()
    {
        // 2% drawdown with 2% threshold → activates
        var eval = CircuitBreaker.Evaluate(98_000m, 100_000m, false, 2m);
        Assert.True(eval.ShouldActivate);
    }

    [Fact]
    public void Evaluate_LooseThreshold_50Percent()
    {
        // 30% drawdown with 50% threshold → no activation
        var eval = CircuitBreaker.Evaluate(70_000m, 100_000m, false, 50m);
        Assert.False(eval.ShouldActivate);
    }

    // ── Evaluate: Regime variations ──

    [Theory]
    [InlineData("Bull", true)]
    [InlineData("Bear", true)]
    [InlineData("Sideways", true)]
    [InlineData("HighVolatility", false)]
    [InlineData("highvolatility", false)] // case-insensitive
    public void Evaluate_RegimeGating(string regime, bool shouldDeactivate)
    {
        var eval = CircuitBreaker.Evaluate(
            currentEquity: 96_000m,
            peakEquity: 100_000m,
            isCurrentlyActive: true,
            recoveryPercent: 5m,
            currentRegime: regime);

        Assert.Equal(shouldDeactivate, eval.ShouldDeactivate);
    }

    // ── Integration: Known scenarios ──

    [Fact]
    public void KnownScenario_2020MarchCrash()
    {
        // Rapid 30% drop → should activate at 15%
        var eval = CircuitBreaker.Evaluate(
            currentEquity: 70_000m,
            peakEquity: 100_000m,
            isCurrentlyActive: false,
            thresholdPercent: 15m,
            currentRegime: "HighVolatility",
            regimeConfidence: 0.95m);

        Assert.True(eval.ShouldActivate);
        Assert.Equal(30m, eval.DrawdownPercent);
        Assert.Equal("HighVolatility", eval.Regime);

        // Recovery attempt in HighVol regime → stay locked
        var eval2 = CircuitBreaker.Evaluate(
            currentEquity: 96_000m,
            peakEquity: 100_000m,
            isCurrentlyActive: true,
            recoveryPercent: 5m,
            currentRegime: "HighVolatility");

        Assert.True(eval2.IsActive);

        // VIX drops, regime shifts to Sideways → deactivate
        var eval3 = CircuitBreaker.Evaluate(
            currentEquity: 96_000m,
            peakEquity: 100_000m,
            isCurrentlyActive: true,
            recoveryPercent: 5m,
            currentRegime: "Sideways");

        Assert.True(eval3.ShouldDeactivate);
    }
}
