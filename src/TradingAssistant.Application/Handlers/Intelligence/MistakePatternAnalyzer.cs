using TradingAssistant.Domain.Intelligence;
using TradingAssistant.Domain.Intelligence.Enums;

namespace TradingAssistant.Application.Handlers.Intelligence;

/// <summary>
/// Pure static analyzer that aggregates TradeReview data to produce
/// mistake distributions, regime breakdowns, and recommendations.
/// </summary>
public static class MistakePatternAnalyzer
{
    /// <summary>Pattern report is generated every N closed trades per market.</summary>
    public const int ReportInterval = 50;

    public record MistakeSummary(
        string MarketCode,
        int TotalTrades,
        int LosingTrades,
        string? MostCommonMistake,
        Dictionary<string, int> MistakeBreakdown,
        Dictionary<string, Dictionary<string, int>> RegimeBreakdown,
        List<string> Recommendations);

    /// <summary>
    /// Analyze a set of trade reviews and produce a mistake summary.
    /// </summary>
    public static MistakeSummary Analyze(string marketCode, IReadOnlyList<TradeReview> reviews)
    {
        var losingReviews = reviews.Where(r => r.PnlPercent < 0 && r.MistakeType.HasValue).ToList();

        var mistakeBreakdown = ComputeMistakeBreakdown(losingReviews);
        var regimeBreakdown = ComputeRegimeBreakdown(losingReviews);
        var mostCommon = mistakeBreakdown.Count > 0
            ? mistakeBreakdown.MaxBy(kv => kv.Value).Key
            : null;
        var recommendations = GenerateRecommendations(mistakeBreakdown, regimeBreakdown, reviews.Count, losingReviews.Count);

        return new MistakeSummary(
            MarketCode: marketCode,
            TotalTrades: reviews.Count,
            LosingTrades: losingReviews.Count,
            MostCommonMistake: mostCommon,
            MistakeBreakdown: mistakeBreakdown,
            RegimeBreakdown: regimeBreakdown,
            Recommendations: recommendations);
    }

    /// <summary>
    /// Count occurrences of each MistakeType.
    /// </summary>
    internal static Dictionary<string, int> ComputeMistakeBreakdown(IReadOnlyList<TradeReview> losingReviews)
    {
        return losingReviews
            .Where(r => r.MistakeType.HasValue)
            .GroupBy(r => r.MistakeType!.Value.ToString())
            .ToDictionary(g => g.Key, g => g.Count());
    }

    /// <summary>
    /// Break down mistakes by regime at entry: { regime → { MistakeType → count } }.
    /// </summary>
    internal static Dictionary<string, Dictionary<string, int>> ComputeRegimeBreakdown(
        IReadOnlyList<TradeReview> losingReviews)
    {
        return losingReviews
            .Where(r => r.MistakeType.HasValue && !string.IsNullOrWhiteSpace(r.RegimeAtEntry))
            .GroupBy(r => r.RegimeAtEntry)
            .ToDictionary(
                g => g.Key,
                g => g.GroupBy(r => r.MistakeType!.Value.ToString())
                      .ToDictionary(mg => mg.Key, mg => mg.Count()));
    }

    /// <summary>
    /// Generate actionable recommendations based on mistake patterns.
    /// </summary>
    internal static List<string> GenerateRecommendations(
        Dictionary<string, int> mistakeBreakdown,
        Dictionary<string, Dictionary<string, int>> regimeBreakdown,
        int totalTrades,
        int losingTrades)
    {
        var recommendations = new List<string>();

        if (mistakeBreakdown.Count == 0)
            return recommendations;

        var topMistake = mistakeBreakdown.MaxBy(kv => kv.Value);
        var topMistakePct = losingTrades > 0 ? (decimal)topMistake.Value / losingTrades * 100 : 0;

        // Recommendation based on most common mistake
        recommendations.Add(topMistake.Key switch
        {
            nameof(MistakeType.StopTooTight) =>
                $"StopTooTight accounts for {topMistakePct:F0}% of losses. Consider widening stop-loss to at least 1.5× ATR.",
            nameof(MistakeType.StopTooLoose) =>
                $"StopTooLoose accounts for {topMistakePct:F0}% of losses. Consider tightening stop-loss placement.",
            nameof(MistakeType.RegimeMismatch) =>
                $"RegimeMismatch accounts for {topMistakePct:F0}% of losses. Avoid trading when regime is transitioning.",
            nameof(MistakeType.OversizedPosition) =>
                $"OversizedPosition accounts for {topMistakePct:F0}% of losses. Reduce position sizing to limit per-trade risk.",
            nameof(MistakeType.BadTiming) =>
                $"BadTiming accounts for {topMistakePct:F0}% of losses. Wait for stronger confirmation signals before entry.",
            nameof(MistakeType.BadSignal) =>
                $"BadSignal accounts for {topMistakePct:F0}% of losses. Review signal filters and add confirmation indicators.",
            nameof(MistakeType.CorrelatedLoss) =>
                $"CorrelatedLoss accounts for {topMistakePct:F0}% of losses. Diversify positions across uncorrelated assets.",
            nameof(MistakeType.BlackSwan) =>
                $"BlackSwan events account for {topMistakePct:F0}% of losses. Consider tail-risk hedging strategies.",
            _ => $"Most common mistake is {topMistake.Key} ({topMistakePct:F0}% of losses)."
        });

        // Regime-specific recommendations
        foreach (var (regime, mistakes) in regimeBreakdown)
        {
            var regimeTotal = mistakes.Values.Sum();
            if (regimeTotal < 3) continue;

            var worstInRegime = mistakes.MaxBy(kv => kv.Value);
            if (worstInRegime.Value >= 3)
            {
                recommendations.Add(
                    $"In {regime} regime, {worstInRegime.Key} is the dominant mistake ({worstInRegime.Value} occurrences). " +
                    $"Consider regime-specific rules for this condition.");
            }
        }

        // Loss rate recommendation
        if (totalTrades > 0)
        {
            var lossRate = (decimal)losingTrades / totalTrades * 100;
            if (lossRate > 60)
            {
                recommendations.Add(
                    $"Overall loss rate is {lossRate:F0}%. Consider pausing trading to review strategy fundamentals.");
            }
        }

        return recommendations;
    }
}
