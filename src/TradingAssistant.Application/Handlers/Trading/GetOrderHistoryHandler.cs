using Microsoft.EntityFrameworkCore;
using TradingAssistant.Application.Exceptions;
using TradingAssistant.Application.Services;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.SharedKernel;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Trading;

public class GetOrderHistoryHandler
{
    public static async Task<PagedResponse<OrderDto>> HandleAsync(
        GetOrderHistoryQuery query,
        TradingDbContext db,
        ICurrentUser currentUser)
    {
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == query.AccountId)
            ?? throw new InvalidOperationException($"Account '{query.AccountId}' not found.");

        if (account.UserId != currentUser.UserId)
            throw new ForbiddenAccessException("You do not have access to this account.");

        var ordersQuery = db.Orders
            .Where(o => o.AccountId == query.AccountId)
            .OrderByDescending(o => o.CreatedAt);

        var totalCount = await ordersQuery.CountAsync();

        var items = await ordersQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(o => new OrderDto(
                o.Id, o.AccountId, o.Symbol,
                o.Side.ToString(), o.Type.ToString(),
                o.Quantity, o.Price, o.Status.ToString(),
                o.CreatedAt, o.FilledAt,
                account.AccountType.ToString()))
            .ToListAsync();

        return new PagedResponse<OrderDto>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount
        };
    }
}
