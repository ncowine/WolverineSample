namespace TradingAssistant.Contracts.DTOs;

/// <summary>
/// Real-time mistake breakdown for a market, computed from trade reviews.
/// </summary>
public record MistakeSummaryDto(
    string MarketCode,
    int TotalTrades,
    int LosingTrades,
    string? MostCommonMistake,
    IReadOnlyDictionary<string, int> MistakeBreakdown,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> RegimeBreakdown,
    IReadOnlyList<string> Recommendations,
    DateTime? LastReportDate,
    int TradesSinceLastReport);

/// <summary>
/// Stored pattern report with Claude analysis.
/// </summary>
public record MistakePatternReportDto(
    Guid Id,
    string MarketCode,
    int TradeCount,
    int LosingTradeCount,
    string MostCommonMistake,
    IReadOnlyDictionary<string, int> MistakeBreakdown,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> RegimeBreakdown,
    IReadOnlyList<string> Recommendations,
    string? ClaudeAnalysis,
    DateTime AnalyzedAt);
