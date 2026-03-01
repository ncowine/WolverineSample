namespace TradingAssistant.Application.Backtesting;

/// <summary>
/// A single point on the equity curve (date + account value).
/// </summary>
public record EquityPoint(DateTime Date, decimal Value);
