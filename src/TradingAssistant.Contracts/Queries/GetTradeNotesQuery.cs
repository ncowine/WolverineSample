namespace TradingAssistant.Contracts.Queries;

public record GetTradeNotesQuery(Guid? OrderId = null, Guid? PositionId = null);
