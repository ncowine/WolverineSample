namespace TradingAssistant.Contracts.DTOs;

public record TradeReviewDto(
    Guid Id,
    Guid TradeId,
    string Symbol,
    string MarketCode,
    string StrategyName,
    decimal EntryPrice,
    decimal ExitPrice,
    DateTime EntryDate,
    DateTime ExitDate,
    decimal PnlPercent,
    decimal PnlAbsolute,
    double DurationHours,
    string RegimeAtEntry,
    string RegimeAtExit,
    decimal? Grade,
    float? MlConfidence,
    string OutcomeClass,
    string? MistakeType,
    int Score,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Weaknesses,
    IReadOnlyList<string> LessonsLearned,
    string Summary,
    DateTime ReviewedAt);

public record ReviewTradeResultDto(
    bool Success,
    Guid? ReviewId,
    string? OutcomeClass,
    string? MistakeType,
    int Score,
    string Summary,
    string? Error = null);
