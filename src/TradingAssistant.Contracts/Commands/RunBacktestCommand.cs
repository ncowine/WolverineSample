namespace TradingAssistant.Contracts.Commands;

public record RunBacktestCommand(
    Guid StrategyId,
    string Symbol,
    DateTime StartDate,
    DateTime EndDate,
    // Portfolio/universe fields (optional — when set, runs as portfolio backtest)
    Guid? UniverseId = null,
    decimal InitialCapital = 100_000m,
    int MaxPositions = 10,
    string CostProfileMarket = "US");
