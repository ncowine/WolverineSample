using System.Text.Json;
using TradingAssistant.Domain.Intelligence;

namespace TradingAssistant.Application.Intelligence;

/// <summary>
/// A decorrelation event: a market pair whose correlation has deviated
/// more than a threshold number of standard deviations from its historical mean.
/// </summary>
public record DecorrelationEvent(
    string MarketA,
    string MarketB,
    decimal CurrentCorrelation,
    decimal HistoricalMean,
    decimal HistoricalStdDev,
    decimal ZScore);

/// <summary>
/// Computes cross-market correlation matrices and detects decorrelation events.
/// Pure static utility — no side effects, no database access.
/// </summary>
public static class CorrelationCalculator
{
    /// <summary>
    /// Compute simple daily returns from close prices.
    /// Returns array of length (closes.Length - 1).
    /// Return[i] = (closes[i+1] - closes[i]) / closes[i].
    /// </summary>
    public static decimal[] ComputeReturns(decimal[] closes)
    {
        if (closes.Length < 2)
            return [];

        var returns = new decimal[closes.Length - 1];
        for (var i = 0; i < returns.Length; i++)
        {
            returns[i] = closes[i] == 0
                ? 0m
                : (closes[i + 1] - closes[i]) / closes[i];
        }

        return returns;
    }

    /// <summary>
    /// Compute Pearson correlation coefficient between two return series.
    /// Uses the last <paramref name="lookbackDays"/> values from each series.
    /// Returns 0 if insufficient data.
    /// </summary>
    public static decimal PearsonCorrelation(decimal[] returnsA, decimal[] returnsB, int lookbackDays = 60)
    {
        var n = Math.Min(returnsA.Length, returnsB.Length);
        if (n < 2)
            return 0m;

        // Use last N values (or all if fewer than lookback)
        var count = Math.Min(n, lookbackDays);
        var offsetA = returnsA.Length - count;
        var offsetB = returnsB.Length - count;

        // Compute means
        var sumA = 0m;
        var sumB = 0m;
        for (var i = 0; i < count; i++)
        {
            sumA += returnsA[offsetA + i];
            sumB += returnsB[offsetB + i];
        }

        var meanA = sumA / count;
        var meanB = sumB / count;

        // Compute covariance and standard deviations
        var cov = 0m;
        var varA = 0m;
        var varB = 0m;

        for (var i = 0; i < count; i++)
        {
            var devA = returnsA[offsetA + i] - meanA;
            var devB = returnsB[offsetB + i] - meanB;
            cov += devA * devB;
            varA += devA * devA;
            varB += devB * devB;
        }

        if (varA == 0 || varB == 0)
            return 0m;

        var correlation = cov / (decimal)(Math.Sqrt((double)(varA * varB)));
        return Math.Round(Math.Max(-1m, Math.Min(1m, correlation)), 4);
    }

    /// <summary>
    /// Build a correlation matrix for all market pairs from their close price series.
    /// </summary>
    /// <param name="marketCloses">Dictionary of MarketCode → daily close prices (ordered chronologically).</param>
    /// <param name="lookbackDays">Rolling window for correlation (default 60 trading days).</param>
    /// <returns>A CorrelationSnapshot with the matrix as JSON, or null if fewer than 2 markets.</returns>
    public static CorrelationSnapshot? ComputeMatrix(
        Dictionary<string, decimal[]> marketCloses,
        DateTime snapshotDate,
        int lookbackDays = 60)
    {
        var markets = marketCloses.Keys.OrderBy(k => k).ToList();
        if (markets.Count < 2)
            return null;

        // Pre-compute returns for each market
        var returns = new Dictionary<string, decimal[]>();
        foreach (var (market, closes) in marketCloses)
        {
            returns[market] = ComputeReturns(closes);
        }

        // Compute pairwise correlations
        var matrix = new Dictionary<string, decimal>();
        for (var i = 0; i < markets.Count; i++)
        {
            for (var j = i + 1; j < markets.Count; j++)
            {
                var key = $"{markets[i]}|{markets[j]}";
                var corr = PearsonCorrelation(returns[markets[i]], returns[markets[j]], lookbackDays);
                matrix[key] = corr;
            }
        }

        return new CorrelationSnapshot
        {
            SnapshotDate = snapshotDate,
            LookbackDays = lookbackDays,
            MatrixJson = JsonSerializer.Serialize(matrix)
        };
    }

    /// <summary>
    /// Parse a CorrelationSnapshot's MatrixJson into a dictionary.
    /// </summary>
    public static Dictionary<string, decimal> ParseMatrix(string matrixJson)
    {
        if (string.IsNullOrWhiteSpace(matrixJson))
            return new Dictionary<string, decimal>();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, decimal>>(matrixJson)
                   ?? new Dictionary<string, decimal>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, decimal>();
        }
    }

    /// <summary>
    /// Detect decorrelation events by comparing current correlation values
    /// against their historical mean and standard deviation.
    /// A decorrelation event fires when |z-score| > sigmaThreshold.
    /// </summary>
    /// <param name="historicalSnapshots">Past snapshots (e.g. 1 year of daily snapshots).</param>
    /// <param name="current">The current/latest snapshot to evaluate.</param>
    /// <param name="sigmaThreshold">Number of standard deviations for alert (default 1.0).</param>
    public static List<DecorrelationEvent> DetectDecorrelations(
        IReadOnlyList<CorrelationSnapshot> historicalSnapshots,
        CorrelationSnapshot current,
        decimal sigmaThreshold = 1.0m)
    {
        var events = new List<DecorrelationEvent>();
        var currentMatrix = ParseMatrix(current.MatrixJson);

        if (currentMatrix.Count == 0 || historicalSnapshots.Count < 2)
            return events;

        // Gather historical values per pair
        var historicalValues = new Dictionary<string, List<decimal>>();
        foreach (var snapshot in historicalSnapshots)
        {
            var matrix = ParseMatrix(snapshot.MatrixJson);
            foreach (var (pair, corr) in matrix)
            {
                if (!historicalValues.ContainsKey(pair))
                    historicalValues[pair] = new List<decimal>();
                historicalValues[pair].Add(corr);
            }
        }

        // Check each current pair against its historical distribution
        foreach (var (pair, currentCorr) in currentMatrix)
        {
            if (!historicalValues.TryGetValue(pair, out var history) || history.Count < 2)
                continue;

            var mean = history.Average();
            var variance = history.Sum(v => (v - mean) * (v - mean)) / (history.Count - 1);
            var stdDev = (decimal)Math.Sqrt((double)variance);

            if (stdDev == 0)
                continue;

            var zScore = (currentCorr - mean) / stdDev;

            if (Math.Abs(zScore) > sigmaThreshold)
            {
                var parts = pair.Split('|');
                events.Add(new DecorrelationEvent(
                    MarketA: parts[0],
                    MarketB: parts.Length > 1 ? parts[1] : pair,
                    CurrentCorrelation: currentCorr,
                    HistoricalMean: Math.Round(mean, 4),
                    HistoricalStdDev: Math.Round(stdDev, 4),
                    ZScore: Math.Round(zScore, 4)));
            }
        }

        return events;
    }
}
