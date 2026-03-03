using TradingAssistant.Domain.Intelligence.Enums;

namespace TradingAssistant.Application.Handlers.Intelligence;

/// <summary>
/// Rule-based heuristic classifier for trade mistakes.
/// Determines the primary MistakeType based on trade context without Claude.
/// Used as default classification; Claude can override for ambiguous cases.
/// </summary>
public static class MistakeClassifier
{
    /// <summary>
    /// Context needed for heuristic classification.
    /// </summary>
    public record TradeContext(
        decimal PnlPercent,
        decimal EntryPrice,
        decimal ExitPrice,
        string RegimeAtEntry,
        string RegimeAtExit,
        OutcomeClass OutcomeClass,
        decimal? AtrAtEntry,
        decimal AverageLossPercent);

    /// <summary>Threshold multiplier for oversized position detection.</summary>
    public const decimal OversizedThreshold = 2.0m;

    /// <summary>
    /// Classify a losing trade's primary mistake using rule-based heuristics.
    /// Priority order: RegimeMismatch → StopTooTight → OversizedPosition → outcome-based → BadSignal.
    /// </summary>
    public static MistakeType Classify(TradeContext context)
    {
        // Rule 1: RegimeMismatch — regime changed during trade
        if (IsRegimeMismatch(context))
            return MistakeType.RegimeMismatch;

        // Rule 2: StopTooTight — stopped out within 1 ATR (outcome was StoppedPrematurely)
        if (IsStopTooTight(context))
            return MistakeType.StopTooTight;

        // Rule 3: OversizedPosition — loss > 2× average loss
        if (IsOversizedPosition(context))
            return MistakeType.OversizedPosition;

        // Rule 4: StopTooLoose — large loss with StoppedCorrectly outcome (stop was too far)
        if (IsStopTooLoose(context))
            return MistakeType.StopTooLoose;

        // Rule 5: Outcome-based fallbacks
        if (context.OutcomeClass == OutcomeClass.GoodEntryBadExit)
            return MistakeType.BadTiming;

        if (context.OutcomeClass == OutcomeClass.BadEntry)
            return MistakeType.BadSignal;

        // Default: BadSignal
        return MistakeType.BadSignal;
    }

    /// <summary>
    /// RegimeMismatch: regime at entry differs from regime at exit.
    /// </summary>
    internal static bool IsRegimeMismatch(TradeContext context)
    {
        if (string.IsNullOrWhiteSpace(context.RegimeAtEntry) ||
            string.IsNullOrWhiteSpace(context.RegimeAtExit))
            return false;

        return !string.Equals(
            context.RegimeAtEntry.Trim(),
            context.RegimeAtExit.Trim(),
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// StopTooTight: stopped out within 1 ATR of entry, and outcome suggests premature stop.
    /// Requires ATR data. The stop distance (|exit - entry|) must be ≤ ATR.
    /// </summary>
    internal static bool IsStopTooTight(TradeContext context)
    {
        if (!context.AtrAtEntry.HasValue || context.AtrAtEntry.Value <= 0)
            return false;

        // Must be a stop-related outcome
        if (context.OutcomeClass != OutcomeClass.StoppedPrematurely)
            return false;

        var stopDistance = Math.Abs(context.ExitPrice - context.EntryPrice);
        return stopDistance <= context.AtrAtEntry.Value;
    }

    /// <summary>
    /// OversizedPosition: loss exceeds 2× the average loss for this market.
    /// </summary>
    internal static bool IsOversizedPosition(TradeContext context)
    {
        if (context.AverageLossPercent >= 0)
            return false; // no meaningful average loss to compare against

        // Both PnlPercent and AverageLossPercent are negative for losses
        // A loss is oversized if its magnitude exceeds 2× the average loss magnitude
        return context.PnlPercent < OversizedThreshold * context.AverageLossPercent;
    }

    /// <summary>
    /// StopTooLoose: outcome was StoppedCorrectly but the loss is notably large
    /// (exceeds 1.5× the average loss), indicating the stop was placed too far away.
    /// </summary>
    internal static bool IsStopTooLoose(TradeContext context)
    {
        if (context.AverageLossPercent >= 0)
            return false;

        if (context.OutcomeClass != OutcomeClass.StoppedCorrectly)
            return false;

        return context.PnlPercent < 1.5m * context.AverageLossPercent;
    }
}
