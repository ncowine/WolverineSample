namespace TradingAssistant.Contracts.DTOs;

/// <summary>
/// Lightweight stock-level regime detection result.
/// </summary>
public record StockRegimeDto(
    string Symbol,
    string Regime,
    string RecommendedTemplate,
    decimal Confidence,
    decimal Sma50Slope,
    decimal Sma200Slope,
    decimal VolatilityRatio,
    string Explanation);
