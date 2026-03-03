using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Domain.Intelligence;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Intelligence;

public class GetAttributionHandler
{
    public static async Task<MonthlyAttributionDto> HandleAsync(
        GetAttributionQuery query,
        IntelligenceDbContext intelligenceDb,
        MarketDataDbContext marketDb,
        ILogger<GetAttributionHandler> logger)
    {
        // Check for cached attribution
        var existing = await intelligenceDb.MonthlyAttributions
            .FirstOrDefaultAsync(a => a.MarketCode == query.MarketCode
                && a.Year == query.Year && a.Month == query.Month);

        if (existing is not null)
            return MapToDto(existing);

        // Compute fresh attribution
        var monthStart = new DateTime(query.Year, query.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);

        // Load trade reviews for this month
        var monthTrades = await intelligenceDb.TradeReviews
            .Where(r => r.MarketCode == query.MarketCode
                && r.ExitDate >= monthStart && r.ExitDate < monthEnd)
            .ToListAsync();

        var strategyReturn = monthTrades.Sum(t => t.PnlPercent);

        // Get benchmark return
        var benchmarkReturn = await GetBenchmarkReturn(
            marketDb, query.MarketCode, monthStart, monthEnd, logger);

        // Get historical monthly returns for beta calculation (up to 12 months back)
        var history = await GetHistoricalReturns(
            intelligenceDb, marketDb, query.MarketCode, query.Year, query.Month, logger);

        var beta = PerformanceAttributor.ComputeBeta(history);

        // Compute regime contribution
        var regimeContribution = PerformanceAttributor.ComputeRegimeContribution(monthTrades);

        var aligned = monthTrades.Count(t =>
            !string.IsNullOrWhiteSpace(t.RegimeAtEntry)
            && string.Equals(t.RegimeAtEntry.Trim(), t.RegimeAtExit.Trim(),
                StringComparison.OrdinalIgnoreCase));
        var mismatched = monthTrades.Count(t =>
            !string.IsNullOrWhiteSpace(t.RegimeAtEntry)
            && !string.IsNullOrWhiteSpace(t.RegimeAtExit)
            && !string.Equals(t.RegimeAtEntry.Trim(), t.RegimeAtExit.Trim(),
                StringComparison.OrdinalIgnoreCase));

        var result = PerformanceAttributor.Attribute(
            query.Year, query.Month, strategyReturn, benchmarkReturn,
            beta, regimeContribution, monthTrades.Count, aligned, mismatched);

        // Save to DB
        var entity = new MonthlyAttribution
        {
            MarketCode = query.MarketCode,
            Year = result.Year,
            Month = result.Month,
            TotalReturn = result.TotalReturn,
            Alpha = result.Alpha,
            BetaContribution = result.BetaContribution,
            RegimeContribution = result.RegimeContribution,
            Residual = result.Residual,
            Beta = result.Beta,
            BenchmarkReturn = result.BenchmarkReturn,
            TradeCount = result.TradeCount,
            RegimeAlignedTrades = result.RegimeAlignedTrades,
            RegimeMismatchedTrades = result.RegimeMismatchedTrades
        };

        intelligenceDb.MonthlyAttributions.Add(entity);
        await intelligenceDb.SaveChangesAsync();

        logger.LogInformation(
            "Attribution computed for {Market} {Year}-{Month:D2}: Total={Total:F2}%, Alpha={Alpha:F2}%, Beta={Beta:F4}",
            query.MarketCode, query.Year, query.Month, result.TotalReturn, result.Alpha, result.Beta);

        return MapToDto(entity);
    }

    public static async Task<RollingAttributionSummaryDto> HandleRollingAsync(
        GetRollingAttributionQuery query,
        IntelligenceDbContext intelligenceDb)
    {
        var attributions = await intelligenceDb.MonthlyAttributions
            .Where(a => a.MarketCode == query.MarketCode)
            .OrderByDescending(a => a.Year * 100 + a.Month)
            .Take(12)
            .ToListAsync();

        var results = attributions
            .OrderBy(a => a.Year * 100 + a.Month)
            .Select(a => new PerformanceAttributor.AttributionResult(
                a.Year, a.Month, a.TotalReturn, a.Alpha, a.BetaContribution,
                a.RegimeContribution, a.Residual, a.Beta, a.BenchmarkReturn,
                a.TradeCount, a.RegimeAlignedTrades, a.RegimeMismatchedTrades))
            .ToList();

        var summary = PerformanceAttributor.BuildRollingSummary(results);

        return new RollingAttributionSummaryDto(
            MonthsIncluded: summary.MonthsIncluded,
            CumulativeReturn: summary.CumulativeReturn,
            CumulativeAlpha: summary.CumulativeAlpha,
            CumulativeBetaContribution: summary.CumulativeBetaContribution,
            CumulativeRegimeContribution: summary.CumulativeRegimeContribution,
            CumulativeResidual: summary.CumulativeResidual,
            AverageBeta: summary.AverageBeta,
            MonthlyResults: summary.MonthlyResults.Select(r => new MonthlyAttributionDto(
                Id: Guid.Empty, MarketCode: query.MarketCode,
                Year: r.Year, Month: r.Month, TotalReturn: r.TotalReturn,
                Alpha: r.Alpha, BetaContribution: r.BetaContribution,
                RegimeContribution: r.RegimeContribution, Residual: r.Residual,
                Beta: r.Beta, BenchmarkReturn: r.BenchmarkReturn,
                TradeCount: r.TradeCount,
                RegimeAlignedTrades: r.RegimeAlignedTrades,
                RegimeMismatchedTrades: r.RegimeMismatchedTrades,
                ComputedAt: DateTime.UtcNow)).ToList());
    }

    private static async Task<decimal> GetBenchmarkReturn(
        MarketDataDbContext marketDb,
        string marketCode,
        DateTime monthStart,
        DateTime monthEnd,
        ILogger logger)
    {
        if (!PerformanceAttributor.BenchmarkSymbols.TryGetValue(marketCode, out var benchSymbol))
            return 0m;

        try
        {
            var stock = await marketDb.Stocks
                .FirstOrDefaultAsync(s => s.Symbol == benchSymbol);

            if (stock is null)
                return 0m;

            var startPrice = await marketDb.PriceCandles
                .Where(c => c.StockId == stock.Id
                    && c.Interval == CandleInterval.Daily
                    && c.Timestamp >= monthStart && c.Timestamp < monthEnd)
                .OrderBy(c => c.Timestamp)
                .Select(c => c.Close)
                .FirstOrDefaultAsync();

            var endPrice = await marketDb.PriceCandles
                .Where(c => c.StockId == stock.Id
                    && c.Interval == CandleInterval.Daily
                    && c.Timestamp >= monthStart && c.Timestamp < monthEnd)
                .OrderByDescending(c => c.Timestamp)
                .Select(c => c.Close)
                .FirstOrDefaultAsync();

            if (startPrice == 0 || endPrice == 0)
                return 0m;

            return (endPrice - startPrice) / startPrice * 100m;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get benchmark return for {Symbol}", benchSymbol);
            return 0m;
        }
    }

    private static async Task<List<PerformanceAttributor.MonthlyReturn>> GetHistoricalReturns(
        IntelligenceDbContext intelligenceDb,
        MarketDataDbContext marketDb,
        string marketCode,
        int year,
        int month,
        ILogger logger)
    {
        var history = new List<PerformanceAttributor.MonthlyReturn>();
        var current = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);

        // Go back up to 12 months
        for (int i = 1; i <= 12; i++)
        {
            var past = current.AddMonths(-i);
            var pastEnd = past.AddMonths(1);

            var monthlyStratReturn = await intelligenceDb.TradeReviews
                .Where(r => r.MarketCode == marketCode
                    && r.ExitDate >= past && r.ExitDate < pastEnd)
                .SumAsync(r => r.PnlPercent);

            var monthlyBenchReturn = await GetBenchmarkReturn(
                marketDb, marketCode, past, pastEnd, logger);

            // Only include months with actual trades
            var tradeCount = await intelligenceDb.TradeReviews
                .CountAsync(r => r.MarketCode == marketCode
                    && r.ExitDate >= past && r.ExitDate < pastEnd);

            if (tradeCount > 0)
            {
                history.Add(new PerformanceAttributor.MonthlyReturn(
                    past.Year, past.Month, monthlyStratReturn, monthlyBenchReturn));
            }
        }

        return history;
    }

    internal static MonthlyAttributionDto MapToDto(MonthlyAttribution a) =>
        new(
            Id: a.Id,
            MarketCode: a.MarketCode,
            Year: a.Year,
            Month: a.Month,
            TotalReturn: a.TotalReturn,
            Alpha: a.Alpha,
            BetaContribution: a.BetaContribution,
            RegimeContribution: a.RegimeContribution,
            Residual: a.Residual,
            Beta: a.Beta,
            BenchmarkReturn: a.BenchmarkReturn,
            TradeCount: a.TradeCount,
            RegimeAlignedTrades: a.RegimeAlignedTrades,
            RegimeMismatchedTrades: a.RegimeMismatchedTrades,
            ComputedAt: a.ComputedAt);
}
