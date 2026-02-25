namespace TradingAssistant.Contracts.Events;

public record LoadHistoricalData(Guid BacktestRunId, Guid StrategyId, string Symbol, DateTime StartDate, DateTime EndDate);
public record HistoricalDataLoaded(Guid BacktestRunId, Guid StrategyId, string Symbol, string PriceDataJson);
public record ExecuteBacktest(Guid BacktestRunId, Guid StrategyId, string Symbol, string PriceDataJson, string RulesJson);
public record BacktestExecuted(Guid BacktestRunId, Guid StrategyId, int TotalTrades, decimal WinRate, decimal TotalReturn, decimal MaxDrawdown, decimal SharpeRatio);
