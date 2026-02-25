namespace TradingAssistant.Contracts.Events;

public record OrderPlaced(
    Guid OrderId,
    Guid AccountId,
    string Symbol,
    string Side,
    decimal Quantity,
    decimal Price);

public record OrderFilled(
    Guid OrderId,
    Guid AccountId,
    string Symbol,
    decimal Quantity,
    decimal Price,
    decimal Fee);

public record OrderCancelled(Guid OrderId, Guid AccountId);
