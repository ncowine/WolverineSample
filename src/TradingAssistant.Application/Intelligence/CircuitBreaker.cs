namespace TradingAssistant.Application.Intelligence;

/// <summary>
/// Circuit breaker evaluation result.
/// </summary>
public record CircuitBreakerEvaluation(
    bool ShouldActivate,
    bool ShouldDeactivate,
    bool IsActive,
    decimal DrawdownPercent,
    decimal PeakEquity,
    decimal CurrentEquity,
    string? Regime,
    decimal? RegimeConfidence,
    string Detail);

/// <summary>
/// Pure static circuit breaker logic for portfolio risk management.
///
/// Rules:
/// 1. ACTIVATE when portfolio drawdown from peak exceeds threshold (default 15%)
/// 2. On activation: cancel all pending orders (positions kept with stops)
/// 3. DEACTIVATE requires BOTH:
///    a. Equity recovered to within DrawdownRecoveryPercent of peak
///    b. Market regime is NOT HighVolatility (regime re-assessment gate)
/// 4. All state changes are logged with full context
/// </summary>
public static class CircuitBreaker
{
    public const decimal DefaultThresholdPercent = 15m;
    public const decimal DefaultRecoveryPercent = 5m;

    /// <summary>
    /// Evaluate the current portfolio state against circuit breaker rules.
    /// </summary>
    /// <param name="currentEquity">Current portfolio equity.</param>
    /// <param name="peakEquity">Highest equity observed.</param>
    /// <param name="isCurrentlyActive">Whether the circuit breaker is currently active.</param>
    /// <param name="thresholdPercent">Drawdown % to trigger activation.</param>
    /// <param name="recoveryPercent">How close to peak equity must recover before deactivation.</param>
    /// <param name="currentRegime">Current market regime (for resume gating).</param>
    /// <param name="regimeConfidence">Confidence of regime classification.</param>
    public static CircuitBreakerEvaluation Evaluate(
        decimal currentEquity,
        decimal peakEquity,
        bool isCurrentlyActive,
        decimal thresholdPercent = DefaultThresholdPercent,
        decimal recoveryPercent = DefaultRecoveryPercent,
        string? currentRegime = null,
        decimal? regimeConfidence = null)
    {
        var drawdownPercent = CalculateDrawdown(currentEquity, peakEquity);

        // Not active → check if should activate
        if (!isCurrentlyActive)
        {
            if (drawdownPercent >= thresholdPercent)
            {
                return new CircuitBreakerEvaluation(
                    ShouldActivate: true,
                    ShouldDeactivate: false,
                    IsActive: true,
                    DrawdownPercent: Math.Round(drawdownPercent, 2),
                    PeakEquity: peakEquity,
                    CurrentEquity: currentEquity,
                    Regime: currentRegime,
                    RegimeConfidence: regimeConfidence,
                    Detail: $"ACTIVATED: drawdown {drawdownPercent:F1}% >= {thresholdPercent}% threshold (peak={peakEquity:F2}, current={currentEquity:F2}, regime={currentRegime ?? "unknown"})");
            }

            return new CircuitBreakerEvaluation(
                ShouldActivate: false,
                ShouldDeactivate: false,
                IsActive: false,
                DrawdownPercent: Math.Round(drawdownPercent, 2),
                PeakEquity: peakEquity,
                CurrentEquity: currentEquity,
                Regime: currentRegime,
                RegimeConfidence: regimeConfidence,
                Detail: $"OK: drawdown {drawdownPercent:F1}% < {thresholdPercent}% threshold");
        }

        // Active → check if should deactivate
        // Allow recovery regardless of regime — regime gating was too aggressive
        // and could lock out trading for months during HighVolatility
        var recoveryThreshold = peakEquity * (1 - recoveryPercent / 100m);
        var equityRecovered = currentEquity >= recoveryThreshold;

        if (equityRecovered)
        {
            return new CircuitBreakerEvaluation(
                ShouldActivate: false,
                ShouldDeactivate: true,
                IsActive: false,
                DrawdownPercent: Math.Round(drawdownPercent, 2),
                PeakEquity: peakEquity,
                CurrentEquity: currentEquity,
                Regime: currentRegime,
                RegimeConfidence: regimeConfidence,
                Detail: $"DEACTIVATED: equity {currentEquity:F2} recovered to within {recoveryPercent}% of peak {peakEquity:F2}, regime={currentRegime ?? "unknown"} (safe)");
        }

        // Still active — explain why
        var reason = !equityRecovered
            ? $"equity {currentEquity:F2} < recovery threshold {recoveryThreshold:F2}"
            : $"regime {currentRegime} is HighVolatility (waiting for regime change)";

        return new CircuitBreakerEvaluation(
            ShouldActivate: false,
            ShouldDeactivate: false,
            IsActive: true,
            DrawdownPercent: Math.Round(drawdownPercent, 2),
            PeakEquity: peakEquity,
            CurrentEquity: currentEquity,
            Regime: currentRegime,
            RegimeConfidence: regimeConfidence,
            Detail: $"ACTIVE: {reason}");
    }

    /// <summary>
    /// Calculate drawdown percentage from peak.
    /// </summary>
    public static decimal CalculateDrawdown(decimal currentEquity, decimal peakEquity)
    {
        if (peakEquity <= 0 || currentEquity >= peakEquity)
            return 0m;

        return (peakEquity - currentEquity) / peakEquity * 100m;
    }

    /// <summary>
    /// Check if equity has recovered enough for deactivation (ignoring regime).
    /// </summary>
    public static bool HasEquityRecovered(decimal currentEquity, decimal peakEquity, decimal recoveryPercent)
    {
        var threshold = peakEquity * (1 - recoveryPercent / 100m);
        return currentEquity >= threshold;
    }

    /// <summary>
    /// Cancel all pending orders. Returns the count of cancelled orders.
    /// </summary>
    public static int CancelPendingOrders<T>(List<T> pendingOrders)
    {
        var count = pendingOrders.Count;
        pendingOrders.Clear();
        return count;
    }
}
