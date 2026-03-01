namespace TradingAssistant.Contracts.DTOs;

public record BacktestComparisonDto(List<BacktestComparisonEntry> Entries);

public record BacktestComparisonEntry(
    Guid BacktestRunId,
    string Symbol,
    string StrategyName,
    DateTime StartDate,
    DateTime EndDate,
    int TotalTrades,
    decimal WinRate,
    decimal TotalReturn,
    decimal Cagr,
    decimal SharpeRatio,
    decimal SortinoRatio,
    decimal CalmarRatio,
    decimal MaxDrawdown,
    decimal ProfitFactor,
    decimal Expectancy,
    decimal? OverfittingScore,
    string? SpyComparisonJson,
    string? ParametersJson);
