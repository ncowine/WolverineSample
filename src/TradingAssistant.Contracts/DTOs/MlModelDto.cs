namespace TradingAssistant.Contracts.DTOs;

public record MlModelDto(
    Guid Id,
    string MarketCode,
    int ModelVersion,
    int FeatureVersion,
    DateTime TrainedAt,
    double Auc,
    double Precision,
    double Recall,
    double F1Score,
    double Accuracy,
    int TrainingSamples,
    int ValidationSamples,
    int WinSamples,
    int LossSamples,
    bool IsActive,
    string? DeactivationReason,
    IReadOnlyList<FeatureImportanceDto>? TopFeatures = null);

public record FeatureImportanceDto(string Name, double Importance);

public record RetrainResultDto(
    bool Success,
    string? FailureReason,
    MlModelDto? Model);
