namespace TradingAssistant.Contracts.Queries;

/// <summary>
/// Load labeled FeatureSnapshots for ML model training.
/// Returns batch of typed FeatureVectors ready for ML.NET pipeline.
/// </summary>
public record GetTrainingDataQuery(
    string? Symbol = null,
    string? MarketCode = null,
    int? MinFeatureVersion = null,
    int MaxRecords = 10_000);
