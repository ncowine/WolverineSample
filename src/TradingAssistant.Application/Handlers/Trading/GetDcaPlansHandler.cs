using Microsoft.EntityFrameworkCore;
using TradingAssistant.Application.Exceptions;
using TradingAssistant.Application.Services;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Trading;

public class GetDcaPlansHandler
{
    public static async Task<List<DcaPlanDto>> HandleAsync(
        GetDcaPlansQuery query,
        TradingDbContext db,
        ICurrentUser currentUser)
    {
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == query.AccountId)
            ?? throw new InvalidOperationException($"Account '{query.AccountId}' not found.");

        if (account.UserId != currentUser.UserId)
            throw new ForbiddenAccessException("You do not have access to this account.");

        return await db.DcaPlans
            .Where(p => p.AccountId == query.AccountId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new DcaPlanDto(
                p.Id,
                p.AccountId,
                p.Symbol,
                p.Amount,
                p.Frequency.ToString(),
                p.NextExecutionDate,
                p.IsActive,
                p.CreatedAt))
            .ToListAsync();
    }
}
