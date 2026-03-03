using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Infrastructure.Caching;

namespace TradingAssistant.Application.Handlers.MarketData;

public class GetStockPriceHandler
{
    public static async Task<StockPriceDto?> HandleAsync(
        GetStockPriceQuery query,
        StockPriceCache cache)
    {
        return await cache.Get(query.Symbol);
    }
}
