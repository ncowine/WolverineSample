namespace TradingAssistant.Contracts.Queries;

public record GetPositionsQuery(Guid AccountId, string? Status = null);
