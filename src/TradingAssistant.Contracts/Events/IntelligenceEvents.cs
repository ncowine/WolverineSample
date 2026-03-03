namespace TradingAssistant.Contracts.Events;

public record RegimeChanged(
    string MarketCode,
    string FromRegime,
    string ToRegime,
    DateTime TransitionDate,
    decimal ConfidenceScore);

/// <summary>
/// Published after a successful ML model training run.
/// </summary>
public record ModelTrained(
    string MarketCode,
    int ModelVersion,
    double Auc,
    bool IsActive,
    string? RollbackReason,
    DateTime TrainedAt);
