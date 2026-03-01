using TradingAssistant.Application.Services;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Domain.MarketData;

namespace TradingAssistant.Tests.Services;

public class CandleAggregatorTests
{
    private static readonly Guid StockId = Guid.NewGuid();

    [Fact]
    public void Weekly_aggregation_groups_by_iso_week()
    {
        // Mon Jan 6 – Fri Jan 10, 2025 (one full week)
        var dailies = new List<PriceCandle>
        {
            MakeDaily(new DateTime(2025, 1, 6), open: 100, high: 110, low: 95, close: 105, volume: 1000),
            MakeDaily(new DateTime(2025, 1, 7), open: 105, high: 112, low: 100, close: 108, volume: 1200),
            MakeDaily(new DateTime(2025, 1, 8), open: 108, high: 115, low: 103, close: 110, volume: 900),
            MakeDaily(new DateTime(2025, 1, 9), open: 110, high: 118, low: 107, close: 115, volume: 1500),
            MakeDaily(new DateTime(2025, 1, 10), open: 115, high: 120, low: 112, close: 118, volume: 1100),
        };

        var weekly = CandleAggregator.AggregateDailyToWeekly(dailies, StockId);

        Assert.Single(weekly);
        var w = weekly[0];
        Assert.Equal(CandleInterval.Weekly, w.Interval);
        Assert.Equal(new DateTime(2025, 1, 6), w.Timestamp); // Monday
        Assert.Equal(100m, w.Open);   // Monday's open
        Assert.Equal(118m, w.Close);  // Friday's close
        Assert.Equal(120m, w.High);   // Max high
        Assert.Equal(95m, w.Low);     // Min low
        Assert.Equal(5700, w.Volume); // Sum
    }

    [Fact]
    public void Weekly_aggregation_handles_partial_weeks()
    {
        // Only Thurs and Fri (partial week, e.g. holiday week)
        var dailies = new List<PriceCandle>
        {
            MakeDaily(new DateTime(2025, 1, 9), open: 100, high: 110, low: 95, close: 105, volume: 1000),
            MakeDaily(new DateTime(2025, 1, 10), open: 105, high: 108, low: 102, close: 107, volume: 800),
        };

        var weekly = CandleAggregator.AggregateDailyToWeekly(dailies, StockId);

        Assert.Single(weekly);
        Assert.Equal(100m, weekly[0].Open);  // First day's open
        Assert.Equal(107m, weekly[0].Close); // Last day's close
    }

    [Fact]
    public void Weekly_aggregation_creates_multiple_weeks()
    {
        // Two weeks of data
        var dailies = new List<PriceCandle>
        {
            // Week 1: Jan 6-10
            MakeDaily(new DateTime(2025, 1, 6), 100, 105, 95, 103, 1000),
            MakeDaily(new DateTime(2025, 1, 7), 103, 108, 100, 106, 1100),
            // Week 2: Jan 13-14
            MakeDaily(new DateTime(2025, 1, 13), 106, 115, 104, 112, 1500),
            MakeDaily(new DateTime(2025, 1, 14), 112, 118, 110, 116, 1300),
        };

        var weekly = CandleAggregator.AggregateDailyToWeekly(dailies, StockId);

        Assert.Equal(2, weekly.Count);
        Assert.Equal(new DateTime(2025, 1, 6), weekly[0].Timestamp);
        Assert.Equal(new DateTime(2025, 1, 13), weekly[1].Timestamp);
    }

    [Fact]
    public void Monthly_aggregation_groups_by_calendar_month()
    {
        var dailies = new List<PriceCandle>
        {
            MakeDaily(new DateTime(2025, 1, 2), 100, 110, 95, 105, 1000),
            MakeDaily(new DateTime(2025, 1, 15), 105, 115, 100, 112, 1200),
            MakeDaily(new DateTime(2025, 1, 31), 112, 120, 108, 118, 900),
        };

        var monthly = CandleAggregator.AggregateDailyToMonthly(dailies, StockId);

        Assert.Single(monthly);
        var m = monthly[0];
        Assert.Equal(CandleInterval.Monthly, m.Interval);
        Assert.Equal(new DateTime(2025, 1, 1), m.Timestamp); // First of month
        Assert.Equal(100m, m.Open);   // First trading day's open
        Assert.Equal(118m, m.Close);  // Last trading day's close
        Assert.Equal(120m, m.High);   // Max
        Assert.Equal(95m, m.Low);     // Min
        Assert.Equal(3100, m.Volume); // Sum
    }

    [Fact]
    public void Monthly_aggregation_creates_multiple_months()
    {
        var dailies = new List<PriceCandle>
        {
            MakeDaily(new DateTime(2025, 1, 10), 100, 110, 95, 105, 1000),
            MakeDaily(new DateTime(2025, 2, 5), 108, 115, 103, 112, 1100),
            MakeDaily(new DateTime(2025, 3, 20), 115, 125, 110, 120, 1300),
        };

        var monthly = CandleAggregator.AggregateDailyToMonthly(dailies, StockId);

        Assert.Equal(3, monthly.Count);
        Assert.Equal(new DateTime(2025, 1, 1), monthly[0].Timestamp);
        Assert.Equal(new DateTime(2025, 2, 1), monthly[1].Timestamp);
        Assert.Equal(new DateTime(2025, 3, 1), monthly[2].Timestamp);
    }

    [Fact]
    public void Weekly_sets_correct_stock_id()
    {
        var dailies = new List<PriceCandle>
        {
            MakeDaily(new DateTime(2025, 1, 6), 100, 105, 95, 103, 1000),
        };

        var weekly = CandleAggregator.AggregateDailyToWeekly(dailies, StockId);

        Assert.Equal(StockId, weekly[0].StockId);
    }

    [Fact]
    public void Monthly_sets_correct_stock_id()
    {
        var dailies = new List<PriceCandle>
        {
            MakeDaily(new DateTime(2025, 1, 6), 100, 105, 95, 103, 1000),
        };

        var monthly = CandleAggregator.AggregateDailyToMonthly(dailies, StockId);

        Assert.Equal(StockId, monthly[0].StockId);
    }

    private static PriceCandle MakeDaily(DateTime date, decimal open, decimal high, decimal low, decimal close, long volume)
    {
        return new PriceCandle
        {
            StockId = StockId,
            Open = open,
            High = high,
            Low = low,
            Close = close,
            Volume = volume,
            Timestamp = date,
            Interval = CandleInterval.Daily
        };
    }
}
