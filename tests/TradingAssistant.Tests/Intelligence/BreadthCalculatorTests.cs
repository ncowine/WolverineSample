using TradingAssistant.Application.Intelligence;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Domain.MarketData;

namespace TradingAssistant.Tests.Intelligence;

public class BreadthCalculatorTests
{
    private static List<PriceCandle> GenerateCandles(
        int count,
        decimal startPrice = 100m,
        decimal dailyChange = 1m,
        DateTime? startDate = null)
    {
        var date = startDate ?? new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        var candles = new List<PriceCandle>(count);
        var price = startPrice;

        for (var i = 0; i < count; i++)
        {
            // Skip weekends
            while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                date = date.AddDays(1);

            var close = price + dailyChange;
            var high = Math.Max(price, close) + 0.50m;
            var low = Math.Min(price, close) - 0.50m;

            candles.Add(new PriceCandle
            {
                StockId = Guid.NewGuid(),
                Open = price,
                High = high,
                Low = low,
                Close = close,
                Volume = 1_000_000,
                Timestamp = date,
                Interval = CandleInterval.Daily
            });

            price = close;
            date = date.AddDays(1);
        }

        return candles;
    }

    private static List<PriceCandle> GenerateFlatCandles(
        int count,
        decimal price = 100m,
        DateTime? startDate = null)
    {
        var date = startDate ?? new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        var candles = new List<PriceCandle>(count);

        for (var i = 0; i < count; i++)
        {
            while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                date = date.AddDays(1);

            candles.Add(new PriceCandle
            {
                StockId = Guid.NewGuid(),
                Open = price,
                High = price + 0.10m,
                Low = price - 0.10m,
                Close = price,
                Volume = 500_000,
                Timestamp = date,
                Interval = CandleInterval.Daily
            });

            date = date.AddDays(1);
        }

        return candles;
    }

    [Fact]
    public void ReturnsNull_WhenUniverseIsEmpty()
    {
        var result = BreadthCalculator.Compute(
            "US_SP500",
            DateTime.UtcNow.Date,
            new Dictionary<string, List<PriceCandle>>());

        Assert.Null(result);
    }

    [Fact]
    public void ReturnsNull_WhenAllStocksHaveNoCandles()
    {
        var stockCandles = new Dictionary<string, List<PriceCandle>>
        {
            ["AAPL"] = new(),
            ["MSFT"] = new()
        };

        var result = BreadthCalculator.Compute("US_SP500", DateTime.UtcNow.Date, stockCandles);

        Assert.Null(result);
    }

    [Fact]
    public void ComputesCorrectAdvancingDeclining_AllAdvancing()
    {
        // 3 stocks, all with last close > previous close (advancing)
        var stockCandles = new Dictionary<string, List<PriceCandle>>
        {
            ["AAPL"] = GenerateCandles(5, startPrice: 100m, dailyChange: 2m),
            ["MSFT"] = GenerateCandles(5, startPrice: 200m, dailyChange: 3m),
            ["GOOGL"] = GenerateCandles(5, startPrice: 150m, dailyChange: 1m)
        };

        var result = BreadthCalculator.Compute("US_SP500", DateTime.UtcNow.Date, stockCandles);

        Assert.NotNull(result);
        Assert.Equal(3, result.Advancing);
        Assert.Equal(0, result.Declining);
        Assert.Equal(3, result.TotalStocks);
    }

    [Fact]
    public void ComputesCorrectAdvancingDeclining_AllDeclining()
    {
        // 3 stocks, all declining (negative daily change)
        var stockCandles = new Dictionary<string, List<PriceCandle>>
        {
            ["AAPL"] = GenerateCandles(5, startPrice: 200m, dailyChange: -2m),
            ["MSFT"] = GenerateCandles(5, startPrice: 300m, dailyChange: -5m),
            ["GOOGL"] = GenerateCandles(5, startPrice: 150m, dailyChange: -1m)
        };

        var result = BreadthCalculator.Compute("US_SP500", DateTime.UtcNow.Date, stockCandles);

        Assert.NotNull(result);
        Assert.Equal(0, result.Advancing);
        Assert.Equal(3, result.Declining);
    }

    [Fact]
    public void ComputesCorrectAdvancingDeclining_Mixed()
    {
        var stockCandles = new Dictionary<string, List<PriceCandle>>
        {
            ["AAPL"] = GenerateCandles(5, startPrice: 100m, dailyChange: 2m),   // advancing
            ["MSFT"] = GenerateCandles(5, startPrice: 200m, dailyChange: -3m),  // declining
            ["GOOGL"] = GenerateFlatCandles(5, price: 150m)                     // unchanged
        };

        var result = BreadthCalculator.Compute("US_SP500", DateTime.UtcNow.Date, stockCandles);

        Assert.NotNull(result);
        Assert.Equal(1, result.Advancing);
        Assert.Equal(1, result.Declining);
        Assert.Equal(3, result.TotalStocks);
    }

    [Fact]
    public void AdvanceDeclineRatio_AllAdvancing_Returns99()
    {
        var stockCandles = new Dictionary<string, List<PriceCandle>>
        {
            ["AAPL"] = GenerateCandles(5, startPrice: 100m, dailyChange: 2m),
            ["MSFT"] = GenerateCandles(5, startPrice: 200m, dailyChange: 3m)
        };

        var result = BreadthCalculator.Compute("US_SP500", DateTime.UtcNow.Date, stockCandles);

        Assert.NotNull(result);
        // When declining == 0 and advancing > 0, ratio caps at 99
        Assert.Equal(99.0m, result.AdvanceDeclineRatio);
    }

    [Fact]
    public void AdvanceDeclineRatio_ComputesCorrectly()
    {
        // 2 advancing, 1 declining → ratio = 2.0
        var stockCandles = new Dictionary<string, List<PriceCandle>>
        {
            ["AAPL"] = GenerateCandles(5, startPrice: 100m, dailyChange: 2m),
            ["MSFT"] = GenerateCandles(5, startPrice: 200m, dailyChange: 3m),
            ["GOOGL"] = GenerateCandles(5, startPrice: 150m, dailyChange: -1m)
        };

        var result = BreadthCalculator.Compute("US_SP500", DateTime.UtcNow.Date, stockCandles);

        Assert.NotNull(result);
        Assert.Equal(2.0m, result.AdvanceDeclineRatio);
    }

    [Fact]
    public void PctAbove200Sma_AllAbove()
    {
        // Steadily rising stocks with 210 candles → all above 200 SMA
        var stockCandles = new Dictionary<string, List<PriceCandle>>
        {
            ["AAPL"] = GenerateCandles(210, startPrice: 100m, dailyChange: 0.5m),
            ["MSFT"] = GenerateCandles(210, startPrice: 200m, dailyChange: 0.3m)
        };

        var result = BreadthCalculator.Compute("US_SP500", DateTime.UtcNow.Date, stockCandles);

        Assert.NotNull(result);
        Assert.Equal(1.0m, result.PctAbove200Sma);
    }

    [Fact]
    public void PctAbove200Sma_AllBelow()
    {
        // Steadily falling stocks with 210 candles → all below 200 SMA
        var stockCandles = new Dictionary<string, List<PriceCandle>>
        {
            ["AAPL"] = GenerateCandles(210, startPrice: 500m, dailyChange: -0.5m),
            ["MSFT"] = GenerateCandles(210, startPrice: 600m, dailyChange: -0.3m)
        };

        var result = BreadthCalculator.Compute("US_SP500", DateTime.UtcNow.Date, stockCandles);

        Assert.NotNull(result);
        Assert.Equal(0m, result.PctAbove200Sma);
    }

    [Fact]
    public void PctAbove50Sma_MixedStocks()
    {
        // One rising (above 50 SMA), one falling (below 50 SMA) → 50%
        var stockCandles = new Dictionary<string, List<PriceCandle>>
        {
            ["AAPL"] = GenerateCandles(60, startPrice: 100m, dailyChange: 0.5m),  // above
            ["MSFT"] = GenerateCandles(60, startPrice: 500m, dailyChange: -0.5m)  // below
        };

        var result = BreadthCalculator.Compute("US_SP500", DateTime.UtcNow.Date, stockCandles);

        Assert.NotNull(result);
        Assert.Equal(0.5m, result.PctAbove50Sma);
    }

    [Fact]
    public void PctAboveSma_IgnoresStocksWithInsufficientHistory()
    {
        // Stock A: 210 candles (enough for 200 SMA), rising → above 200 SMA
        // Stock B: only 30 candles (not enough for 50 or 200 SMA)
        var stockCandles = new Dictionary<string, List<PriceCandle>>
        {
            ["AAPL"] = GenerateCandles(210, startPrice: 100m, dailyChange: 0.5m),
            ["NEW_IPO"] = GenerateCandles(30, startPrice: 50m, dailyChange: 0.2m)
        };

        var result = BreadthCalculator.Compute("US_SP500", DateTime.UtcNow.Date, stockCandles);

        Assert.NotNull(result);
        Assert.Equal(2, result.TotalStocks);
        // Only 1 stock qualifies for 200 SMA check, and it's above → 0.5 (1/2)
        Assert.Equal(0.5m, result.PctAbove200Sma);
        // Only 1 stock qualifies for 50 SMA check, and it's above → 0.5 (1/2)
        Assert.Equal(0.5m, result.PctAbove50Sma);
    }

    [Fact]
    public void NewHighs_DetectedCorrectly()
    {
        // Steadily rising → last day hits 52-week high
        var candles = GenerateCandles(260, startPrice: 50m, dailyChange: 0.2m);

        var stockCandles = new Dictionary<string, List<PriceCandle>>
        {
            ["AAPL"] = candles
        };

        var result = BreadthCalculator.Compute("US_SP500", DateTime.UtcNow.Date, stockCandles);

        Assert.NotNull(result);
        Assert.Equal(1, result.NewHighs);
        Assert.Equal(0, result.NewLows);
    }

    [Fact]
    public void NewLows_DetectedCorrectly()
    {
        // Steadily falling → last day hits 52-week low
        var candles = GenerateCandles(260, startPrice: 500m, dailyChange: -0.2m);

        var stockCandles = new Dictionary<string, List<PriceCandle>>
        {
            ["AAPL"] = candles
        };

        var result = BreadthCalculator.Compute("US_SP500", DateTime.UtcNow.Date, stockCandles);

        Assert.NotNull(result);
        Assert.Equal(0, result.NewHighs);
        Assert.Equal(1, result.NewLows);
    }

    [Fact]
    public void NewHighsAndLows_FlatStock_BothDetected()
    {
        // Flat stock: last high == 52wk high AND last low == 52wk low
        var candles = GenerateFlatCandles(260, price: 100m);

        var stockCandles = new Dictionary<string, List<PriceCandle>>
        {
            ["FLAT"] = candles
        };

        var result = BreadthCalculator.Compute("US_SP500", DateTime.UtcNow.Date, stockCandles);

        Assert.NotNull(result);
        // Flat stock: high = 100.10 every day, low = 99.90 every day
        // Last day high == max high → new high; last day low == min low → new low
        Assert.Equal(1, result.NewHighs);
        Assert.Equal(1, result.NewLows);
    }

    [Fact]
    public void HandlesStockWithOnlyOneCandle()
    {
        var candles = GenerateCandles(1, startPrice: 100m);

        var stockCandles = new Dictionary<string, List<PriceCandle>>
        {
            ["AAPL"] = candles
        };

        var result = BreadthCalculator.Compute("US_SP500", DateTime.UtcNow.Date, stockCandles);

        Assert.NotNull(result);
        Assert.Equal(1, result.TotalStocks);
        // Only 1 candle → can't compute advance/decline (need 2)
        Assert.Equal(0, result.Advancing);
        Assert.Equal(0, result.Declining);
        // Not enough for SMA
        Assert.Equal(0m, result.PctAbove200Sma);
        Assert.Equal(0m, result.PctAbove50Sma);
        // Only 1 candle → it's both the high and the low
        Assert.Equal(1, result.NewHighs);
        Assert.Equal(1, result.NewLows);
    }

    [Fact]
    public void SetsMarketCodeAndSnapshotDate()
    {
        var date = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var stockCandles = new Dictionary<string, List<PriceCandle>>
        {
            ["AAPL"] = GenerateCandles(5)
        };

        var result = BreadthCalculator.Compute("IN_NIFTY50", date, stockCandles);

        Assert.NotNull(result);
        Assert.Equal("IN_NIFTY50", result.MarketCode);
        Assert.Equal(date, result.SnapshotDate);
    }

    [Fact]
    public void AdvanceDeclineRatio_AllUnchanged_Returns1()
    {
        // All flat → 0 advancing, 0 declining → ratio = 1.0
        var stockCandles = new Dictionary<string, List<PriceCandle>>
        {
            ["AAPL"] = GenerateFlatCandles(5),
            ["MSFT"] = GenerateFlatCandles(5)
        };

        var result = BreadthCalculator.Compute("US_SP500", DateTime.UtcNow.Date, stockCandles);

        Assert.NotNull(result);
        Assert.Equal(0, result.Advancing);
        Assert.Equal(0, result.Declining);
        Assert.Equal(1.0m, result.AdvanceDeclineRatio);
    }

    [Fact]
    public void LargeUniverse_ComputesCorrectPercentages()
    {
        // 10 stocks: 7 rising, 3 falling
        var stockCandles = new Dictionary<string, List<PriceCandle>>();
        for (var i = 0; i < 7; i++)
            stockCandles[$"UP_{i}"] = GenerateCandles(60, startPrice: 100m + i * 10, dailyChange: 0.5m);
        for (var i = 0; i < 3; i++)
            stockCandles[$"DOWN_{i}"] = GenerateCandles(60, startPrice: 300m + i * 10, dailyChange: -0.5m);

        var result = BreadthCalculator.Compute("US_SP500", DateTime.UtcNow.Date, stockCandles);

        Assert.NotNull(result);
        Assert.Equal(10, result.TotalStocks);
        Assert.Equal(7, result.Advancing);
        Assert.Equal(3, result.Declining);
        Assert.Equal(Math.Round(7m / 3m, 4), result.AdvanceDeclineRatio);
        // All 7 rising stocks above 50 SMA, all 3 falling below → 70%
        Assert.Equal(0.7m, result.PctAbove50Sma);
    }

    [Fact]
    public void ShortHistory_Under52Weeks_UsesAvailableData()
    {
        // Only 20 candles: still computes 52-week high/low from available data
        var candles = GenerateCandles(20, startPrice: 100m, dailyChange: 1m);

        var stockCandles = new Dictionary<string, List<PriceCandle>>
        {
            ["AAPL"] = candles
        };

        var result = BreadthCalculator.Compute("US_SP500", DateTime.UtcNow.Date, stockCandles);

        Assert.NotNull(result);
        // Rising stock: last candle's high is the highest → new high
        Assert.Equal(1, result.NewHighs);
        Assert.Equal(0, result.NewLows);
    }
}
