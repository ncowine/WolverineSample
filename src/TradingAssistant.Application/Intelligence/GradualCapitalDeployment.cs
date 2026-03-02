namespace TradingAssistant.Application.Intelligence;

/// <summary>
/// Deployment phases for gradual capital ramp-up.
/// </summary>
public enum DeploymentPhase
{
    /// <summary>25% of target allocation — initial phase after promotion.</summary>
    Initial25,

    /// <summary>50% of target allocation — after proving early profitability.</summary>
    Ramp50,

    /// <summary>100% of target allocation — fully deployed.</summary>
    Full100
}

/// <summary>
/// Immutable state snapshot for a strategy's capital deployment progress.
/// </summary>
public record DeploymentState(
    DeploymentPhase Phase,
    decimal AllocationPercent,
    int ProfitableTradeCount,
    DateTime PromotionDate,
    decimal PeakEquitySincePromotion,
    string Detail);

/// <summary>
/// Result of evaluating whether a deployment phase should change.
/// </summary>
public record DeploymentEvaluation(
    DeploymentPhase NewPhase,
    decimal NewAllocationPercent,
    bool PhaseChanged,
    bool DrawdownScaleDown,
    string Detail);

/// <summary>
/// Pure static calculator for gradual capital deployment.
///
/// Rules:
/// 1. Promoted strategy starts at 25% of target allocation (Initial25)
/// 2. Scales to 50% after 10 profitable trades AND 2 weeks since promotion
/// 3. Scales to 100% after 25 profitable trades AND 4 weeks since promotion
/// 4. Immediate scale-down to 25% if drawdown exceeds 5% during ramp (not at Full100)
/// 5. "Whichever later" means BOTH conditions must be met to advance
/// </summary>
public static class GradualCapitalDeployment
{
    public const decimal InitialAllocationPercent = 25m;
    public const decimal Ramp50AllocationPercent = 50m;
    public const decimal FullAllocationPercent = 100m;

    public const int TradesForRamp50 = 10;
    public const int TradesForFull100 = 25;
    public const int DaysForRamp50 = 14;
    public const int DaysForFull100 = 28;
    public const decimal DrawdownScaleDownThreshold = 5m;

    /// <summary>
    /// Create initial deployment state for a newly promoted strategy.
    /// </summary>
    public static DeploymentState CreateInitial(DateTime promotionDate)
    {
        return new DeploymentState(
            Phase: DeploymentPhase.Initial25,
            AllocationPercent: InitialAllocationPercent,
            ProfitableTradeCount: 0,
            PromotionDate: promotionDate,
            PeakEquitySincePromotion: 0m,
            Detail: "Newly promoted — starting at 25% allocation");
    }

    /// <summary>
    /// Evaluate whether deployment phase should change.
    /// Call after each trade or periodically.
    /// </summary>
    /// <param name="currentPhase">Current deployment phase.</param>
    /// <param name="profitableTradeCount">Total profitable trades since promotion.</param>
    /// <param name="currentDate">Current evaluation date.</param>
    /// <param name="promotionDate">Date the strategy was promoted.</param>
    /// <param name="currentEquity">Current portfolio equity for this strategy.</param>
    /// <param name="peakEquity">Peak equity since promotion (for drawdown check).</param>
    public static DeploymentEvaluation Evaluate(
        DeploymentPhase currentPhase,
        int profitableTradeCount,
        DateTime currentDate,
        DateTime promotionDate,
        decimal currentEquity,
        decimal peakEquity)
    {
        var daysSincePromotion = (int)(currentDate.Date - promotionDate.Date).TotalDays;

        // Check drawdown scale-down (only during ramp phases, not at Full100)
        if (currentPhase != DeploymentPhase.Full100 && peakEquity > 0)
        {
            var drawdownPercent = (peakEquity - currentEquity) / peakEquity * 100m;
            if (drawdownPercent > DrawdownScaleDownThreshold)
            {
                return new DeploymentEvaluation(
                    NewPhase: DeploymentPhase.Initial25,
                    NewAllocationPercent: InitialAllocationPercent,
                    PhaseChanged: currentPhase != DeploymentPhase.Initial25,
                    DrawdownScaleDown: true,
                    Detail: $"SCALE-DOWN: drawdown {drawdownPercent:F1}% exceeds {DrawdownScaleDownThreshold}% threshold during ramp — resetting to 25% (peak={peakEquity:F2}, current={currentEquity:F2})");
            }
        }

        // Check for phase advancement
        switch (currentPhase)
        {
            case DeploymentPhase.Initial25:
                if (profitableTradeCount >= TradesForRamp50 && daysSincePromotion >= DaysForRamp50)
                {
                    // Check if we can skip straight to Full100
                    if (profitableTradeCount >= TradesForFull100 && daysSincePromotion >= DaysForFull100)
                    {
                        return new DeploymentEvaluation(
                            NewPhase: DeploymentPhase.Full100,
                            NewAllocationPercent: FullAllocationPercent,
                            PhaseChanged: true,
                            DrawdownScaleDown: false,
                            Detail: $"ADVANCED to 100%: {profitableTradeCount} profitable trades (>={TradesForFull100}) AND {daysSincePromotion} days (>={DaysForFull100})");
                    }

                    return new DeploymentEvaluation(
                        NewPhase: DeploymentPhase.Ramp50,
                        NewAllocationPercent: Ramp50AllocationPercent,
                        PhaseChanged: true,
                        DrawdownScaleDown: false,
                        Detail: $"ADVANCED to 50%: {profitableTradeCount} profitable trades (>={TradesForRamp50}) AND {daysSincePromotion} days (>={DaysForRamp50})");
                }

                return NoChange(currentPhase, InitialAllocationPercent,
                    profitableTradeCount, daysSincePromotion);

            case DeploymentPhase.Ramp50:
                if (profitableTradeCount >= TradesForFull100 && daysSincePromotion >= DaysForFull100)
                {
                    return new DeploymentEvaluation(
                        NewPhase: DeploymentPhase.Full100,
                        NewAllocationPercent: FullAllocationPercent,
                        PhaseChanged: true,
                        DrawdownScaleDown: false,
                        Detail: $"ADVANCED to 100%: {profitableTradeCount} profitable trades (>={TradesForFull100}) AND {daysSincePromotion} days (>={DaysForFull100})");
                }

                return NoChange(currentPhase, Ramp50AllocationPercent,
                    profitableTradeCount, daysSincePromotion);

            case DeploymentPhase.Full100:
                return new DeploymentEvaluation(
                    NewPhase: DeploymentPhase.Full100,
                    NewAllocationPercent: FullAllocationPercent,
                    PhaseChanged: false,
                    DrawdownScaleDown: false,
                    Detail: "Fully deployed at 100%");

            default:
                return NoChange(currentPhase, InitialAllocationPercent,
                    profitableTradeCount, daysSincePromotion);
        }
    }

    /// <summary>
    /// Apply allocation percent as a multiplier to a position size.
    /// Returns the adjusted share count (rounded down).
    /// </summary>
    public static int ApplyAllocation(int targetShares, decimal allocationPercent)
    {
        if (targetShares <= 0 || allocationPercent <= 0) return 0;
        if (allocationPercent >= 100m) return targetShares;
        return (int)(targetShares * allocationPercent / 100m);
    }

    private static DeploymentEvaluation NoChange(
        DeploymentPhase phase, decimal allocation,
        int profitableTrades, int days)
    {
        var (neededTrades, neededDays) = phase switch
        {
            DeploymentPhase.Initial25 => (TradesForRamp50, DaysForRamp50),
            DeploymentPhase.Ramp50 => (TradesForFull100, DaysForFull100),
            _ => (0, 0)
        };

        return new DeploymentEvaluation(
            NewPhase: phase,
            NewAllocationPercent: allocation,
            PhaseChanged: false,
            DrawdownScaleDown: false,
            Detail: $"HOLD at {allocation}%: {profitableTrades}/{neededTrades} trades, {days}/{neededDays} days");
    }
}
