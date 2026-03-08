using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Infrastructure.Caching;

public class BacktestResultCache : DataCache<Guid, BacktestResultDto>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public BacktestResultCache(IServiceScopeFactory scopeFactory)
        : base(new CacheOptions
        {
            AbsoluteExpiration = TimeSpan.FromHours(2),
            UnusedThreshold = TimeSpan.FromMinutes(30),
            PurgeInterval = TimeSpan.FromMinutes(5),
            MaxItems = 200
        })
    {
        _scopeFactory = scopeFactory;
    }

    internal override async Task<BacktestResultDto> FetchAsync(Guid key, CancellationToken ct)
    {
        var result = await FetchAsync(new HashSet<Guid> { key }, ct);
        return result.TryGetValue(key, out var dto) ? dto : default!;
    }

    internal override async Task<IReadOnlyDictionary<Guid, BacktestResultDto>> FetchAsync(
        HashSet<Guid> keys, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BacktestDbContext>();

        var result = new Dictionary<Guid, BacktestResultDto>();

        var runs = await db.BacktestRuns
            .Include(r => r.Result)
            .Where(r => keys.Contains(r.Id))
            .ToListAsync(ct);

        foreach (var run in runs)
        {
            if (run.Result is null)
                continue;

            var r = run.Result;
            result[run.Id] = new BacktestResultDto(
                r.Id, run.Id, run.StrategyId,
                run.Symbol, run.Status.ToString(),
                r.TotalTrades, r.WinRate,
                r.TotalReturn, r.MaxDrawdown, r.SharpeRatio,
                run.StartDate, run.EndDate, run.CreatedAt,
                r.Cagr, r.SortinoRatio,
                r.CalmarRatio, r.ProfitFactor,
                r.Expectancy, r.OverfittingScore,
                r.EquityCurveJson, r.TradeLogJson,
                r.MonthlyReturnsJson, r.BenchmarkReturnJson,
                r.ParametersJson, r.WalkForwardJson,
                r.SpyComparisonJson,
                // Portfolio fields
                run.UniverseId, run.UniverseName,
                run.InitialCapital, run.MaxPositions,
                run.TotalSymbols, run.SymbolsWithData,
                r.UniqueSymbolsTraded, r.AveragePositionsHeld,
                r.MaxPositionsHeld, r.SymbolBreakdownJson,
                r.ExecutionLogJson, r.RegimeTimelineJson);
        }

        return result;
    }
}
