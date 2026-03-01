using Microsoft.EntityFrameworkCore;
using TradingAssistant.Application.Exceptions;
using TradingAssistant.Application.Services;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Trading;

public class GetPositionsHandler
{
    public static async Task<List<PositionDto>> HandleAsync(
        GetPositionsQuery query,
        TradingDbContext db,
        ICurrentUser currentUser)
    {
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == query.AccountId)
            ?? throw new InvalidOperationException($"Account '{query.AccountId}' not found.");

        if (account.UserId != currentUser.UserId)
            throw new ForbiddenAccessException("You do not have access to this account.");

        var positionsQuery = db.Positions
            .Where(p => p.AccountId == query.AccountId);

        if (!string.IsNullOrEmpty(query.Status) && Enum.TryParse<PositionStatus>(query.Status, true, out var status))
        {
            positionsQuery = positionsQuery.Where(p => p.Status == status);
        }

        return await positionsQuery
            .OrderByDescending(p => p.OpenedAt)
            .Select(p => new PositionDto(
                p.Id, p.AccountId, p.Symbol,
                p.Quantity, p.AverageEntryPrice, p.CurrentPrice,
                (p.CurrentPrice - p.AverageEntryPrice) * p.Quantity,
                p.Status.ToString(), p.OpenedAt, p.ClosedAt,
                account.AccountType.ToString()))
            .ToListAsync();
    }
}
