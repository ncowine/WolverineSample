namespace TradingAssistant.Contracts.DTOs;

public record MonthlyAttributionDto(
    Guid Id,
    string MarketCode,
    int Year,
    int Month,
    decimal TotalReturn,
    decimal Alpha,
    decimal BetaContribution,
    decimal RegimeContribution,
    decimal Residual,
    decimal Beta,
    decimal BenchmarkReturn,
    int TradeCount,
    int RegimeAlignedTrades,
    int RegimeMismatchedTrades,
    DateTime ComputedAt);

public record RollingAttributionSummaryDto(
    int MonthsIncluded,
    decimal CumulativeReturn,
    decimal CumulativeAlpha,
    decimal CumulativeBetaContribution,
    decimal CumulativeRegimeContribution,
    decimal CumulativeResidual,
    decimal AverageBeta,
    IReadOnlyList<MonthlyAttributionDto> MonthlyResults);
