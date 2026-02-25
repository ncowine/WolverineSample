namespace TradingAssistant.Contracts.Events;

public record PositionOpened(
    Guid PositionId,
    Guid AccountId,
    string Symbol,
    decimal Quantity,
    decimal EntryPrice);

public record PositionClosed(
    Guid PositionId,
    Guid AccountId,
    string Symbol,
    decimal Quantity,
    decimal ExitPrice,
    decimal PnL);
