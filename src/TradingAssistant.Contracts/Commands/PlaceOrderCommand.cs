namespace TradingAssistant.Contracts.Commands;

public record PlaceOrderCommand(
    Guid AccountId,
    string Symbol,
    string Side,      // "Buy" or "Sell"
    string Type,      // "Market" or "Limit"
    decimal Quantity,
    decimal? Price);
