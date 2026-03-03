using TradingAssistant.Application.Indicators;
using TradingAssistant.Domain.Intelligence;
using TradingAssistant.Domain.Intelligence.Enums;
using TradingAssistant.Infrastructure.ML;

namespace TradingAssistant.Application.Handlers.Intelligence;

/// <summary>
/// Bridges FeatureExtractor (dictionary-based) with FeatureVector (ML.NET typed).
/// Handles direct extraction, batch conversion from stored snapshots, and
/// graceful fallback for missing indicator values (warmup period).
/// </summary>
public static class FeatureVectorConverter
{
    /// <summary>
    /// Extract a typed FeatureVector directly from indicators + context.
    /// Handles null indicators gracefully (warmup period defaults to zeros).
    /// </summary>
    public static FeatureVector Extract(
        IndicatorValues? indicators, FeatureExtractor.FeatureContext context,
        bool label = false)
    {
        var safeIndicators = indicators ?? new IndicatorValues();
        var features = FeatureExtractor.ExtractFeatures(safeIndicators, context);
        return FeatureVector.FromDictionary(features, label);
    }

    /// <summary>
    /// Convert a stored FeatureSnapshot (compressed JSON) into a typed FeatureVector.
    /// Returns null if decompression fails.
    /// </summary>
    public static FeatureVector? FromSnapshot(FeatureSnapshot snapshot)
    {
        var features = FeatureExtractor.DecompressFeatures(snapshot.FeaturesJson);
        if (features is null) return null;

        var isWin = snapshot.TradeOutcome == TradeOutcome.Win;
        return FeatureVector.FromDictionary(features, isWin);
    }

    /// <summary>
    /// Batch-convert N FeatureSnapshots into N FeatureVectors for ML training.
    /// Skips snapshots that fail decompression. Only includes labeled data
    /// (Win or Loss outcomes) unless includeUnlabeled is true.
    /// </summary>
    public static List<FeatureVector> BatchConvert(
        IEnumerable<FeatureSnapshot> snapshots, bool includeUnlabeled = false)
    {
        var vectors = new List<FeatureVector>();

        foreach (var snapshot in snapshots)
        {
            if (!includeUnlabeled && snapshot.TradeOutcome == TradeOutcome.Pending)
                continue;

            var vector = FromSnapshot(snapshot);
            if (vector is not null)
                vectors.Add(vector);
        }

        return vectors;
    }
}
