using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.SharedKernel;
using Wolverine;

namespace TradingAssistant.Api.Endpoints;

public class AuditEndpoints : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/audit-logs", GetAuditLogs)
            .WithTags("Audit")
            .WithSummary("Query audit logs with optional filters")
            .RequireAuthorization();
    }

    private static async Task<PagedResponse<AuditLogDto>> GetAuditLogs(
        string? entityType,
        string? entityId,
        string? action,
        DateTime? startDate,
        DateTime? endDate,
        int page,
        int pageSize,
        IMessageBus bus)
    {
        return await bus.InvokeAsync<PagedResponse<AuditLogDto>>(
            new GetAuditLogsQuery(
                entityType, entityId, action, startDate, endDate,
                page > 0 ? page : 1,
                pageSize > 0 ? pageSize : 20));
    }
}
