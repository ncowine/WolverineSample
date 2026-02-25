namespace TradingAssistant.Contracts.Queries;

public record GetOrderHistoryQuery(Guid AccountId, int Page = 1, int PageSize = 20);
