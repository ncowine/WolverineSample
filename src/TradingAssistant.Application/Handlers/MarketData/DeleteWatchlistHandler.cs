using Microsoft.EntityFrameworkCore;
using TradingAssistant.Application.Exceptions;
using TradingAssistant.Application.Services;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.MarketData;

public class DeleteWatchlistHandler
{
    public static async Task<string> HandleAsync(
        DeleteWatchlistCommand command,
        MarketDataDbContext db,
        ICurrentUser currentUser)
    {
        var watchlist = await db.Watchlists
            .FirstOrDefaultAsync(w => w.Id == command.WatchlistId)
            ?? throw new InvalidOperationException($"Watchlist '{command.WatchlistId}' not found.");

        if (watchlist.UserId != currentUser.UserId)
            throw new ForbiddenAccessException("You do not have access to this watchlist.");

        db.Watchlists.Remove(watchlist);
        await db.SaveChangesAsync();

        return "Watchlist deleted successfully.";
    }
}
