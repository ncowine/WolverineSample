namespace TradingAssistant.Contracts.DTOs;

public record MarketRegimeDto(
    Guid Id,
    string MarketCode,
    string CurrentRegime,
    DateTime RegimeStartDate,
    int RegimeDuration,
    decimal SmaSlope50,
    decimal SmaSlope200,
    decimal VixLevel,
    decimal BreadthScore,
    decimal PctAbove200Sma,
    decimal AdvanceDeclineRatio,
    decimal ConfidenceScore,
    DateTime ClassifiedAt);
