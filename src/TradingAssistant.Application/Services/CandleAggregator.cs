using TradingAssistant.Domain.Enums;
using TradingAssistant.Domain.MarketData;

namespace TradingAssistant.Application.Services;

/// <summary>
/// Aggregates daily candles into weekly and monthly candles.
/// </summary>
public static class CandleAggregator
{
    /// <summary>
    /// Aggregates daily candles into weekly candles.
    /// Weekly: Monday open → Friday close, high = max, low = min, volume = sum.
    /// Partial weeks (e.g. holidays) are still aggregated.
    /// </summary>
    public static List<PriceCandle> AggregateDailyToWeekly(IReadOnlyList<PriceCandle> dailyCandles, Guid stockId)
    {
        return dailyCandles
            .OrderBy(c => c.Timestamp)
            .GroupBy(c => GetIsoWeekStart(c.Timestamp))
            .Select(week => new PriceCandle
            {
                StockId = stockId,
                Open = week.First().Open,
                High = week.Max(c => c.High),
                Low = week.Min(c => c.Low),
                Close = week.Last().Close,
                Volume = week.Sum(c => c.Volume),
                Timestamp = week.Key, // Monday of the week
                Interval = CandleInterval.Weekly
            })
            .ToList();
    }

    /// <summary>
    /// Aggregates daily candles into monthly candles.
    /// Monthly: first trading day open → last trading day close, high = max, low = min, volume = sum.
    /// </summary>
    public static List<PriceCandle> AggregateDailyToMonthly(IReadOnlyList<PriceCandle> dailyCandles, Guid stockId)
    {
        return dailyCandles
            .OrderBy(c => c.Timestamp)
            .GroupBy(c => new { c.Timestamp.Year, c.Timestamp.Month })
            .Select(month => new PriceCandle
            {
                StockId = stockId,
                Open = month.First().Open,
                High = month.Max(c => c.High),
                Low = month.Min(c => c.Low),
                Close = month.Last().Close,
                Volume = month.Sum(c => c.Volume),
                Timestamp = new DateTime(month.Key.Year, month.Key.Month, 1, 0, 0, 0, DateTimeKind.Utc),
                Interval = CandleInterval.Monthly
            })
            .ToList();
    }

    /// <summary>
    /// Returns the Monday (ISO week start) for a given date.
    /// </summary>
    private static DateTime GetIsoWeekStart(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.Date.AddDays(-diff);
    }
}
