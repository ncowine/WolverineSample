namespace TradingAssistant.Contracts.DTOs;

public record TradeChartDataDto(
    string Symbol,
    List<TradeChartCandle> Candles,
    decimal EntryPrice,
    decimal ExitPrice,
    string EntryDate,
    string ExitDate,
    string ExitReason,
    string? ReasoningJson,
    decimal SignalScore,
    string? Regime,
    int Shares,
    decimal PnL,
    decimal PnLPercent,
    decimal StopLossPrice,
    decimal TakeProfitPrice,
    int HoldingDays,
    decimal Commission);

public record TradeChartCandle(
    string Date,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume);
