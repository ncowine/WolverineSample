using Microsoft.EntityFrameworkCore;
using TradingAssistant.Application.Exceptions;
using TradingAssistant.Application.Services;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Infrastructure.Caching;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Trading;

public class GetPortfolioHandler
{
    public static async Task<PortfolioDto> HandleAsync(
        GetPortfolioQuery query,
        PortfolioCache cache,
        TradingDbContext db,
        ICurrentUser currentUser)
    {
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == query.AccountId)
            ?? throw new InvalidOperationException($"Account '{query.AccountId}' not found.");

        if (account.UserId != currentUser.UserId)
            throw new ForbiddenAccessException("You do not have access to this account.");

        var result = await cache.Get(query.AccountId);
        if (result is null)
            throw new InvalidOperationException($"Portfolio for account '{query.AccountId}' not found.");
        return result;
    }
}
