namespace TradingAssistant.Contracts.DTOs;

/// <summary>
/// Result of feature drift detection between training data and recent data.
/// </summary>
public record FeatureDriftResultDto(
    IReadOnlyList<FeatureDriftEntry> DriftedFeatures,
    bool SignificantDriftDetected,
    int TrainingWindowSize,
    int RecentWindowSize);

/// <summary>
/// A single feature's drift measurement.
/// </summary>
public record FeatureDriftEntry(
    string FeatureName,
    double TrainingMean,
    double RecentMean,
    double TrainingStdDev,
    double RecentStdDev,
    double DriftMagnitude,
    bool IsSignificant);
