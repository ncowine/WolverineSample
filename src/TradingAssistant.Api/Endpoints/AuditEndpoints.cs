using Microsoft.AspNetCore.Authorization;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.SharedKernel;
using Wolverine;
using Wolverine.Http;

namespace TradingAssistant.Api.Endpoints;

public static class AuditEndpoints
{
    [Authorize]
    [WolverineGet("/api/audit-logs")]
    public static async Task<PagedResponse<AuditLogDto>> GetAuditLogs(
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
