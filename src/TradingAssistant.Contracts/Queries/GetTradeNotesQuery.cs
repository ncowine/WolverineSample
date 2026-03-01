namespace TradingAssistant.Contracts.Queries;

public record GetTradeNotesQuery(
    Guid? OrderId = null,
    Guid? PositionId = null,
    string? Tag = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    int Page = 1,
    int PageSize = 20);
