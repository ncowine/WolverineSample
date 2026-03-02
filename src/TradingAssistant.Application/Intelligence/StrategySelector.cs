using TradingAssistant.Domain.Intelligence;
using TradingAssistant.Domain.Intelligence.Enums;

namespace TradingAssistant.Application.Intelligence;

/// <summary>
/// Pure strategy selection logic — no DB dependency, fully backtestable.
/// Selects the best strategy for a given regime based on Sharpe scores
/// and computes gradual switchover allocation.
/// </summary>
public static class StrategySelector
{
    /// <summary>
    /// Default number of days over which a new strategy ramps from 50% to 100% allocation.
    /// </summary>
    public const int SwitchoverDays = 5;

    /// <summary>
    /// Starting allocation when a new strategy is assigned.
    /// </summary>
    public const decimal StartAllocation = 50m;

    /// <summary>
    /// Full allocation after switchover completes.
    /// </summary>
    public const decimal FullAllocation = 100m;

    /// <summary>
    /// Select the best strategy for the given regime from a collection of regime scores.
    /// Returns null if no strategies have scores for the regime.
    /// </summary>
    public static StrategyRegimeScore? SelectBest(
        IReadOnlyList<StrategyRegimeScore> scores,
        RegimeType regime)
    {
        if (scores.Count == 0) return null;

        var candidates = scores
            .Where(s => s.Regime == regime)
            .OrderByDescending(s => s.SharpeRatio)
            .ThenByDescending(s => s.SampleSize)
            .ToList();

        return candidates.FirstOrDefault();
    }

    /// <summary>
    /// Compute the current allocation percentage based on days since switchover started.
    /// Ramps linearly from 50% (day 0) to 100% (day SwitchoverDays).
    /// </summary>
    public static decimal ComputeAllocation(DateTime switchoverStart, DateTime currentDate)
    {
        var daysSinceSwitch = (currentDate.Date - switchoverStart.Date).Days;

        if (daysSinceSwitch <= 0) return StartAllocation;
        if (daysSinceSwitch >= SwitchoverDays) return FullAllocation;

        // Linear interpolation: 50% + (50% * daysSinceSwitch / SwitchoverDays)
        var ramp = (FullAllocation - StartAllocation) * daysSinceSwitch / SwitchoverDays;
        return Math.Round(StartAllocation + ramp, 2);
    }

    /// <summary>
    /// Determine whether an existing assignment should be replaced.
    /// Returns true if:
    /// - No current assignment exists
    /// - Current assignment is not locked
    /// - Current assignment is for a different regime
    /// </summary>
    public static bool ShouldReplace(StrategyAssignment? current, RegimeType newRegime)
    {
        if (current is null) return true;
        if (current.IsLocked) return false;
        return current.Regime != newRegime;
    }
}
