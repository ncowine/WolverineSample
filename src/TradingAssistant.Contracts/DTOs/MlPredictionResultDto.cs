namespace TradingAssistant.Contracts.DTOs;

/// <summary>
/// Result of an ML prediction for a symbol.
/// </summary>
public record MlPredictionResultDto(
    string MarketCode,
    string Symbol,
    float? Confidence,
    bool ModelAvailable,
    string? ModelPath,
    int? ModelVersion);
