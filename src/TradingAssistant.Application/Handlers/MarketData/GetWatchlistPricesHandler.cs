using Microsoft.EntityFrameworkCore;
using TradingAssistant.Application.Exceptions;
using TradingAssistant.Application.Services;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Infrastructure.Caching;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.MarketData;

public class GetWatchlistPricesHandler
{
    public static async Task<List<StockPriceDto>> HandleAsync(
        GetWatchlistPricesQuery query,
        MarketDataDbContext db,
        StockPriceCache priceCache,
        ICurrentUser currentUser)
    {
        var watchlist = await db.Watchlists
            .Include(w => w.Items)
            .FirstOrDefaultAsync(w => w.Id == query.WatchlistId)
            ?? throw new InvalidOperationException($"Watchlist '{query.WatchlistId}' not found.");

        if (watchlist.UserId != currentUser.UserId)
            throw new ForbiddenAccessException("You do not have access to this watchlist.");

        if (watchlist.Items.Count == 0)
            return new List<StockPriceDto>();

        var symbols = watchlist.Items.Select(i => i.Symbol).ToHashSet();
        var prices = await priceCache.Get(symbols);

        return prices.Values.OrderBy(p => p.Symbol).ToList();
    }
}
