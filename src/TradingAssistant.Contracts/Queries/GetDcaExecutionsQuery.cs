namespace TradingAssistant.Contracts.Queries;

public record GetDcaExecutionsQuery(Guid PlanId, int Page = 1, int PageSize = 20);
