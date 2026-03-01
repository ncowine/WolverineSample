using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Infrastructure.Caching;

namespace TradingAssistant.Tests.Helpers;

public class FakeStockPriceCache : StockPriceCache
{
    private readonly Dictionary<string, StockPriceDto> _prices = new();

    public FakeStockPriceCache() : base(null!)
    {
    }

    public void SetPrice(string symbol, decimal price)
    {
        _prices[symbol] = new StockPriceDto(symbol, symbol, price, 0m, 0m, 0, DateTime.UtcNow);
    }

    internal override Task<StockPriceDto> FetchAsync(string key, CancellationToken ct)
    {
        _prices.TryGetValue(key, out var dto);
        return Task.FromResult(dto!);
    }

    internal override Task<IReadOnlyDictionary<string, StockPriceDto>> FetchAsync(
        HashSet<string> keys, CancellationToken ct)
    {
        var result = new Dictionary<string, StockPriceDto>();
        foreach (var key in keys)
        {
            if (_prices.TryGetValue(key, out var dto))
                result[key] = dto;
        }
        return Task.FromResult<IReadOnlyDictionary<string, StockPriceDto>>(result);
    }
}
