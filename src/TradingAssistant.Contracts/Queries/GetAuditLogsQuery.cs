namespace TradingAssistant.Contracts.Queries;

public record GetAuditLogsQuery(
    string? EntityType = null,
    string? EntityId = null,
    string? Action = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    int Page = 1,
    int PageSize = 20);
