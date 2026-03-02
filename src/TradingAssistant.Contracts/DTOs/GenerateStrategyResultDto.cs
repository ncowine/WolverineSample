namespace TradingAssistant.Contracts.DTOs;

/// <summary>
/// Result of AI strategy generation including backtest validation.
/// </summary>
public record GenerateStrategyResultDto(
    bool Success,
    bool Rejected,
    StrategyV2Dto? Strategy,
    BacktestSummaryDto? BacktestMetrics,
    string Rationale,
    string? RejectionReason = null);

/// <summary>
/// Key backtest metrics for the generated strategy.
/// </summary>
public record BacktestSummaryDto(
    int TotalTrades,
    decimal WinRate,
    decimal TotalReturn,
    decimal MaxDrawdown,
    decimal SharpeRatio);
