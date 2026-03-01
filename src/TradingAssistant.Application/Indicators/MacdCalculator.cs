namespace TradingAssistant.Application.Indicators;

/// <summary>
/// MACD (Moving Average Convergence Divergence).
/// MACD Line = EMA(fast) - EMA(slow)
/// Signal Line = EMA(signal period) of MACD Line
/// Histogram = MACD Line - Signal Line
/// Default parameters: fast=12, slow=26, signal=9.
/// </summary>
public class MacdCalculator
{
    public static readonly MacdCalculator Instance = new();

    public MacdResult Calculate(decimal[] prices, int fastPeriod = 12, int slowPeriod = 26, int signalPeriod = 9)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(fastPeriod, 1, nameof(fastPeriod));
        ArgumentOutOfRangeException.ThrowIfLessThan(slowPeriod, 1, nameof(slowPeriod));
        ArgumentOutOfRangeException.ThrowIfLessThan(signalPeriod, 1, nameof(signalPeriod));

        if (fastPeriod >= slowPeriod)
            throw new ArgumentException("Fast period must be less than slow period.");

        var length = prices.Length;
        var macd = new decimal[length];
        var signal = new decimal[length];
        var histogram = new decimal[length];

        if (length < slowPeriod)
            return new MacdResult(macd, signal, histogram);

        // Compute fast and slow EMAs
        var fastEma = EmaCalculator.Instance.Calculate(prices, fastPeriod);
        var slowEma = EmaCalculator.Instance.Calculate(prices, slowPeriod);

        // MACD line = fastEMA - slowEMA (only valid after slowPeriod warmup)
        for (var i = slowPeriod - 1; i < length; i++)
        {
            macd[i] = fastEma[i] - slowEma[i];
        }

        // Signal line = EMA of MACD line
        // We need to apply EMA to the non-zero portion of MACD
        // The MACD becomes valid at index (slowPeriod - 1)
        // Signal needs (signalPeriod) valid MACD values, so first signal at (slowPeriod - 1 + signalPeriod - 1)
        var signalStartIndex = slowPeriod - 1 + signalPeriod - 1;

        if (signalStartIndex < length)
        {
            // Seed: SMA of first signalPeriod valid MACD values
            var macdSum = 0m;
            for (var i = slowPeriod - 1; i < slowPeriod - 1 + signalPeriod; i++)
                macdSum += macd[i];

            signal[signalStartIndex] = macdSum / signalPeriod;

            // Apply EMA formula
            var k = 2m / (signalPeriod + 1);
            for (var i = signalStartIndex + 1; i < length; i++)
            {
                signal[i] = macd[i] * k + signal[i - 1] * (1 - k);
            }

            // Histogram = MACD - Signal
            for (var i = signalStartIndex; i < length; i++)
            {
                histogram[i] = macd[i] - signal[i];
            }
        }

        return new MacdResult(macd, signal, histogram);
    }
}

public record MacdResult(decimal[] Macd, decimal[] Signal, decimal[] Histogram);
