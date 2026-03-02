using TradingAssistant.Application.Indicators;
using TradingAssistant.Domain.Intelligence;
using TradingAssistant.Domain.MarketData;

namespace TradingAssistant.Application.Intelligence;

/// <summary>
/// Computes market breadth indicators for a stock universe on a given date.
/// Breadth measures: advance/decline ratio, % above 200/50 SMA, new 52-week highs/lows.
/// </summary>
public static class BreadthCalculator
{
    private const int Sma50Period = 50;
    private const int Sma200Period = 200;
    private const int WeekHighLowLookback = 252; // ~52 weeks of trading days

    /// <summary>
    /// Computes a BreadthSnapshot for the given market on the specified date.
    /// </summary>
    /// <param name="marketCode">Market identifier (e.g. "US_SP500").</param>
    /// <param name="snapshotDate">The date to compute breadth for.</param>
    /// <param name="stockCandles">
    /// Dictionary mapping stock symbol to its daily PriceCandles, ordered by Timestamp ascending.
    /// Only Daily-interval candles up to and including snapshotDate should be provided.
    /// </param>
    /// <returns>A populated BreadthSnapshot, or null if the universe is empty.</returns>
    public static BreadthSnapshot? Compute(
        string marketCode,
        DateTime snapshotDate,
        Dictionary<string, List<PriceCandle>> stockCandles)
    {
        if (stockCandles.Count == 0)
            return null;

        var advancing = 0;
        var declining = 0;
        var above200Sma = 0;
        var above50Sma = 0;
        var newHighs = 0;
        var newLows = 0;
        var totalStocks = 0;

        foreach (var (symbol, candles) in stockCandles)
        {
            if (candles.Count == 0)
                continue;

            totalStocks++;

            var closes = candles.Select(c => c.Close).ToArray();
            var highs = candles.Select(c => c.High).ToArray();
            var lows = candles.Select(c => c.Low).ToArray();
            var lastIndex = closes.Length - 1;
            var currentClose = closes[lastIndex];

            // Advance / Decline: compare latest close to previous close
            if (closes.Length >= 2)
            {
                var previousClose = closes[lastIndex - 1];
                if (currentClose > previousClose)
                    advancing++;
                else if (currentClose < previousClose)
                    declining++;
                // unchanged stocks count toward neither
            }

            // % above 200 SMA
            if (closes.Length >= Sma200Period)
            {
                var sma200 = SmaCalculator.Instance.Calculate(closes, Sma200Period);
                if (sma200[lastIndex] > 0 && currentClose > sma200[lastIndex])
                    above200Sma++;
            }

            // % above 50 SMA
            if (closes.Length >= Sma50Period)
            {
                var sma50 = SmaCalculator.Instance.Calculate(closes, Sma50Period);
                if (sma50[lastIndex] > 0 && currentClose > sma50[lastIndex])
                    above50Sma++;
            }

            // New 52-week high / low
            var lookbackStart = Math.Max(0, closes.Length - WeekHighLowLookback);
            var lookbackHighs = highs.AsSpan(lookbackStart);
            var lookbackLows = lows.AsSpan(lookbackStart);

            var high52Week = MaxOfSpan(lookbackHighs);
            var low52Week = MinOfSpan(lookbackLows);

            // Current day's high equals the 52-week high → new high
            if (highs[lastIndex] >= high52Week)
                newHighs++;

            // Current day's low equals the 52-week low → new low
            if (lows[lastIndex] <= low52Week)
                newLows++;
        }

        if (totalStocks == 0)
            return null;

        // A/D ratio: advancing / declining (guard against division by zero)
        var adRatio = declining > 0
            ? Math.Round((decimal)advancing / declining, 4)
            : advancing > 0 ? 99.0m : 1.0m;

        return new BreadthSnapshot
        {
            MarketCode = marketCode,
            SnapshotDate = snapshotDate,
            AdvanceDeclineRatio = adRatio,
            PctAbove200Sma = Math.Round((decimal)above200Sma / totalStocks, 4),
            PctAbove50Sma = Math.Round((decimal)above50Sma / totalStocks, 4),
            NewHighs = newHighs,
            NewLows = newLows,
            TotalStocks = totalStocks,
            Advancing = advancing,
            Declining = declining
        };
    }

    private static decimal MaxOfSpan(ReadOnlySpan<decimal> values)
    {
        var max = decimal.MinValue;
        foreach (var v in values)
            if (v > max) max = v;
        return max;
    }

    private static decimal MinOfSpan(ReadOnlySpan<decimal> values)
    {
        var min = decimal.MaxValue;
        foreach (var v in values)
            if (v < min) min = v;
        return min;
    }
}
