using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Infrastructure.Caching;

public class StockPriceCache : DataCache<string, StockPriceDto>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public StockPriceCache(IServiceScopeFactory scopeFactory)
        : base(new CacheOptions
        {
            AbsoluteExpiration = TimeSpan.FromSeconds(30),
            UnusedThreshold = TimeSpan.FromSeconds(15),
            PurgeInterval = TimeSpan.FromSeconds(10),
            MaxItems = 100
        })
    {
        _scopeFactory = scopeFactory;
    }

    internal override async Task<StockPriceDto> FetchAsync(string key, CancellationToken ct)
    {
        var result = await FetchAsync(new HashSet<string> { key }, ct);
        return result.TryGetValue(key, out var dto) ? dto : default!;
    }

    internal override async Task<IReadOnlyDictionary<string, StockPriceDto>> FetchAsync(
        HashSet<string> keys, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketDataDbContext>();

        var result = new Dictionary<string, StockPriceDto>();

        foreach (var symbol in keys)
        {
            var stock = await db.Stocks
                .FirstOrDefaultAsync(s => s.Symbol == symbol, ct);

            if (stock is null)
                continue;

            var latestCandle = await db.PriceCandles
                .Where(c => c.StockId == stock.Id)
                .OrderByDescending(c => c.Timestamp)
                .FirstOrDefaultAsync(ct);

            var previousCandle = await db.PriceCandles
                .Where(c => c.StockId == stock.Id)
                .OrderByDescending(c => c.Timestamp)
                .Skip(1)
                .FirstOrDefaultAsync(ct);

            var currentPrice = latestCandle?.Close ?? 0m;
            var previousPrice = previousCandle?.Close ?? currentPrice;
            var change = currentPrice - previousPrice;
            var changePercent = previousPrice != 0 ? (change / previousPrice) * 100 : 0;

            result[symbol] = new StockPriceDto(
                stock.Symbol,
                stock.Name,
                currentPrice,
                Math.Round(change, 2),
                Math.Round(changePercent, 2),
                latestCandle?.Volume ?? 0,
                latestCandle?.Timestamp ?? DateTime.UtcNow);
        }

        return result;
    }
}
