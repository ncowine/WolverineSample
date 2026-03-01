using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Infrastructure.Caching;

public class PortfolioCache : DataCache<Guid, PortfolioDto>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public PortfolioCache(IServiceScopeFactory scopeFactory)
        : base(new CacheOptions
        {
            AbsoluteExpiration = TimeSpan.FromSeconds(30),
            UnusedThreshold = TimeSpan.FromSeconds(15),
            PurgeInterval = TimeSpan.FromSeconds(10),
            MaxItems = 50
        })
    {
        _scopeFactory = scopeFactory;
    }

    internal override async Task<PortfolioDto> FetchAsync(Guid key, CancellationToken ct)
    {
        var result = await FetchAsync(new HashSet<Guid> { key }, ct);
        return result.TryGetValue(key, out var dto) ? dto : default!;
    }

    internal override async Task<IReadOnlyDictionary<Guid, PortfolioDto>> FetchAsync(
        HashSet<Guid> keys, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TradingDbContext>();

        var result = new Dictionary<Guid, PortfolioDto>();

        var portfolios = await db.Portfolios
            .Include(p => p.Account)
            .Where(p => keys.Contains(p.AccountId))
            .ToListAsync(ct);

        foreach (var portfolio in portfolios)
        {
            result[portfolio.AccountId] = new PortfolioDto(
                portfolio.Id,
                portfolio.AccountId,
                portfolio.TotalValue,
                portfolio.CashBalance,
                portfolio.InvestedValue,
                portfolio.TotalPnL,
                portfolio.LastUpdatedAt,
                portfolio.Account.AccountType.ToString());
        }

        return result;
    }
}
