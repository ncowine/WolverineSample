using Microsoft.EntityFrameworkCore;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Infrastructure.Persistence;
using TradingAssistant.SharedKernel;

namespace TradingAssistant.Application.Handlers.Audit;

public class GetAuditLogsHandler
{
    public static async Task<PagedResponse<AuditLogDto>> HandleAsync(
        GetAuditLogsQuery query,
        TradingDbContext db)
    {
        var logsQuery = db.AuditLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.EntityType))
            logsQuery = logsQuery.Where(a => a.EntityType == query.EntityType);

        if (!string.IsNullOrWhiteSpace(query.EntityId))
            logsQuery = logsQuery.Where(a => a.EntityId == query.EntityId);

        if (!string.IsNullOrWhiteSpace(query.Action))
            logsQuery = logsQuery.Where(a => a.Action == query.Action);

        if (query.StartDate.HasValue)
            logsQuery = logsQuery.Where(a => a.Timestamp >= query.StartDate.Value);

        if (query.EndDate.HasValue)
            logsQuery = logsQuery.Where(a => a.Timestamp <= query.EndDate.Value);

        logsQuery = logsQuery.OrderByDescending(a => a.Timestamp);

        var totalCount = await logsQuery.CountAsync();

        var page = query.Page > 0 ? query.Page : 1;
        var pageSize = query.PageSize > 0 ? query.PageSize : 20;

        var items = await logsQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditLogDto(
                a.Id, a.EntityType, a.EntityId, a.Action,
                a.OldValues, a.NewValues, a.UserId, a.Timestamp))
            .ToListAsync();

        return new PagedResponse<AuditLogDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }
}
