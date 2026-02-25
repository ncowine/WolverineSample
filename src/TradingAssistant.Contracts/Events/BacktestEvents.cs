namespace TradingAssistant.Contracts.Events;

public record BacktestRequested(Guid BacktestRunId, Guid StrategyId, string Symbol);
public record BacktestCompleted(Guid BacktestRunId, Guid StrategyId, decimal TotalReturn, decimal WinRate);
public record StrategyCreated(Guid StrategyId, string Name);
public record StrategySignalGenerated(Guid StrategyId, string Symbol, string SignalType, decimal Price);
