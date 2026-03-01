using Microsoft.EntityFrameworkCore;
using TradingAssistant.Application.Exceptions;
using TradingAssistant.Application.Services;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Infrastructure.Persistence;
using TradingAssistant.SharedKernel;

namespace TradingAssistant.Application.Handlers.Trading;

public class GetDcaExecutionsHandler
{
    public static async Task<PagedResponse<DcaExecutionDto>> HandleAsync(
        GetDcaExecutionsQuery query,
        TradingDbContext db,
        ICurrentUser currentUser)
    {
        var plan = await db.DcaPlans
            .Include(p => p.Account)
            .FirstOrDefaultAsync(p => p.Id == query.PlanId)
            ?? throw new InvalidOperationException($"DCA plan '{query.PlanId}' not found.");

        if (plan.Account.UserId != currentUser.UserId)
            throw new ForbiddenAccessException("You do not have access to this DCA plan.");

        var baseQuery = db.DcaExecutions
            .Where(e => e.DcaPlanId == query.PlanId)
            .OrderByDescending(e => e.CreatedAt);

        var totalCount = await baseQuery.CountAsync();

        var items = await baseQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(e => new DcaExecutionDto(
                e.Id,
                e.DcaPlanId,
                e.OrderId,
                e.Amount,
                e.ExecutedPrice,
                e.Quantity,
                e.Status.ToString(),
                e.ErrorReason,
                e.CreatedAt))
            .ToListAsync();

        return new PagedResponse<DcaExecutionDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }
}
