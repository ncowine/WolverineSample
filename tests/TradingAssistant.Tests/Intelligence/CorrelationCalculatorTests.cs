using TradingAssistant.Application.Intelligence;
using TradingAssistant.Domain.Intelligence;

namespace TradingAssistant.Tests.Intelligence;

public class CorrelationCalculatorTests
{
    // ── ComputeReturns ──

    [Fact]
    public void ComputeReturns_SimplePrices_CorrectReturns()
    {
        var closes = new[] { 100m, 110m, 105m, 115m };

        var returns = CorrelationCalculator.ComputeReturns(closes);

        Assert.Equal(3, returns.Length);
        Assert.Equal(0.10m, returns[0]);   // (110-100)/100
        Assert.Equal(105m / 110m - 1m, returns[1]); // (105-110)/110 ≈ -0.0455
        Assert.True(returns[2] > 0); // 115 > 105 → positive
    }

    [Fact]
    public void ComputeReturns_SinglePrice_ReturnsEmpty()
    {
        var returns = CorrelationCalculator.ComputeReturns([100m]);

        Assert.Empty(returns);
    }

    [Fact]
    public void ComputeReturns_EmptyPrices_ReturnsEmpty()
    {
        var returns = CorrelationCalculator.ComputeReturns([]);

        Assert.Empty(returns);
    }

    [Fact]
    public void ComputeReturns_ZeroPrice_ReturnsZeroForThatDay()
    {
        var closes = new[] { 0m, 100m, 110m };

        var returns = CorrelationCalculator.ComputeReturns(closes);

        Assert.Equal(0m, returns[0]); // division by zero guarded
        Assert.Equal(0.10m, returns[1]);
    }

    // ── PearsonCorrelation ──

    [Fact]
    public void PearsonCorrelation_PerfectPositive_ReturnsOne()
    {
        // Identical return series → correlation = 1.0
        var returns = new[] { 0.01m, -0.02m, 0.03m, -0.01m, 0.02m, 0.005m, -0.015m, 0.025m, -0.005m, 0.01m };

        var corr = CorrelationCalculator.PearsonCorrelation(returns, returns);

        Assert.Equal(1.0m, corr);
    }

    [Fact]
    public void PearsonCorrelation_PerfectNegative_ReturnsMinusOne()
    {
        // Opposite return series → correlation = -1.0
        var returnsA = new[] { 0.01m, -0.02m, 0.03m, -0.01m, 0.02m, 0.005m, -0.015m, 0.025m, -0.005m, 0.01m };
        var returnsB = returnsA.Select(r => -r).ToArray();

        var corr = CorrelationCalculator.PearsonCorrelation(returnsA, returnsB);

        Assert.Equal(-1.0m, corr);
    }

    [Fact]
    public void PearsonCorrelation_WeaklyCorrelated_ModerateMagnitude()
    {
        // Two series with weak/no systematic relationship
        var returnsA = new[] { 0.01m, -0.02m, 0.03m, -0.01m, 0.02m, -0.03m, 0.01m, -0.02m, 0.03m, -0.01m };
        var returnsB = new[] { 0.02m, 0.01m, -0.01m, 0.03m, -0.02m, 0.01m, -0.03m, 0.02m, -0.01m, 0.03m };

        var corr = CorrelationCalculator.PearsonCorrelation(returnsA, returnsB);

        // Correlation should be between -1 and 1, and not perfectly correlated
        Assert.True(corr > -1.0m && corr < 1.0m, $"Expected |corr| < 1 but got {corr}");
        Assert.True(Math.Abs(corr) < 0.95m, $"Expected not near-perfect but got {corr}");
    }

    [Fact]
    public void PearsonCorrelation_HighPositive_KnownScenario()
    {
        // Two markets that move together with some noise
        var baseReturns = new[] { 0.01m, -0.02m, 0.015m, -0.005m, 0.02m, -0.01m, 0.03m, -0.015m, 0.01m, 0.005m };
        var noisyReturns = baseReturns.Select(r => r + 0.002m).ToArray(); // shift doesn't affect correlation

        var corr = CorrelationCalculator.PearsonCorrelation(baseReturns, noisyReturns);

        Assert.Equal(1.0m, corr); // adding a constant doesn't change correlation
    }

    [Fact]
    public void PearsonCorrelation_InsufficientData_ReturnsZero()
    {
        var corr = CorrelationCalculator.PearsonCorrelation([0.01m], [0.02m]);

        Assert.Equal(0m, corr);
    }

    [Fact]
    public void PearsonCorrelation_ConstantSeries_ReturnsZero()
    {
        // Zero variance → can't compute correlation
        var constant = Enumerable.Repeat(0.01m, 10).ToArray();
        var other = new[] { 0.01m, -0.02m, 0.03m, -0.01m, 0.02m, -0.03m, 0.01m, -0.02m, 0.03m, -0.01m };

        var corr = CorrelationCalculator.PearsonCorrelation(constant, other);

        Assert.Equal(0m, corr);
    }

    [Fact]
    public void PearsonCorrelation_UsesLookbackWindow()
    {
        // Long series but only last 5 values used
        var returnsA = new[] { 0.10m, 0.20m, 0.30m, 0.01m, -0.02m, 0.03m, -0.01m, 0.02m };
        var returnsB = new[] { -0.10m, -0.20m, -0.30m, 0.01m, -0.02m, 0.03m, -0.01m, 0.02m };

        // Last 5 values are identical → correlation should be 1.0
        var corr = CorrelationCalculator.PearsonCorrelation(returnsA, returnsB, lookbackDays: 5);

        Assert.Equal(1.0m, corr);
    }

    [Fact]
    public void PearsonCorrelation_DifferentLengthSeries_UsesMinimum()
    {
        var returnsA = new[] { 0.01m, -0.02m, 0.03m, -0.01m, 0.02m };
        var returnsB = new[] { 0.01m, -0.02m, 0.03m };

        // Should use min(5,3) = 3 values from the end of each
        var corr = CorrelationCalculator.PearsonCorrelation(returnsA, returnsB);

        // Last 3 of A: [0.03, -0.01, 0.02], last 3 of B: [0.01, -0.02, 0.03]
        Assert.True(corr != 0, "Should compute a non-zero correlation");
    }

    // ── ComputeMatrix ──

    [Fact]
    public void ComputeMatrix_TwoMarkets_ProducesOnePair()
    {
        var marketCloses = new Dictionary<string, decimal[]>
        {
            ["US_SP500"] = GenerateRisingPrices(70, 100m, 0.5m),
            ["IN_NIFTY50"] = GenerateRisingPrices(70, 200m, 0.3m)
        };

        var snapshot = CorrelationCalculator.ComputeMatrix(
            marketCloses,
            new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.NotNull(snapshot);
        Assert.Equal(60, snapshot.LookbackDays);

        var matrix = CorrelationCalculator.ParseMatrix(snapshot.MatrixJson);
        Assert.Single(matrix);
        Assert.True(matrix.ContainsKey("IN_NIFTY50|US_SP500")); // alphabetical order
    }

    [Fact]
    public void ComputeMatrix_ThreeMarkets_ProducesThreePairs()
    {
        var marketCloses = new Dictionary<string, decimal[]>
        {
            ["US_SP500"] = GenerateRisingPrices(70, 100m, 0.5m),
            ["IN_NIFTY50"] = GenerateRisingPrices(70, 200m, 0.3m),
            ["UK_FTSE100"] = GenerateRisingPrices(70, 150m, 0.4m)
        };

        var snapshot = CorrelationCalculator.ComputeMatrix(
            marketCloses,
            new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.NotNull(snapshot);
        var matrix = CorrelationCalculator.ParseMatrix(snapshot.MatrixJson);
        Assert.Equal(3, matrix.Count); // C(3,2) = 3 pairs
    }

    [Fact]
    public void ComputeMatrix_SingleMarket_ReturnsNull()
    {
        var marketCloses = new Dictionary<string, decimal[]>
        {
            ["US_SP500"] = GenerateRisingPrices(70, 100m, 0.5m)
        };

        var snapshot = CorrelationCalculator.ComputeMatrix(
            marketCloses,
            DateTime.UtcNow.Date);

        Assert.Null(snapshot);
    }

    [Fact]
    public void ComputeMatrix_IdenticalMarkets_CorrelationOne()
    {
        var prices = GenerateRisingPrices(70, 100m, 0.5m);
        var marketCloses = new Dictionary<string, decimal[]>
        {
            ["MARKET_A"] = prices,
            ["MARKET_B"] = (decimal[])prices.Clone()
        };

        var snapshot = CorrelationCalculator.ComputeMatrix(
            marketCloses,
            DateTime.UtcNow.Date);

        Assert.NotNull(snapshot);
        var matrix = CorrelationCalculator.ParseMatrix(snapshot.MatrixJson);
        Assert.Equal(1.0m, matrix["MARKET_A|MARKET_B"]);
    }

    [Fact]
    public void ComputeMatrix_OppositeReturns_StrongNegativeCorrelation()
    {
        // Construct prices where returns are exactly opposite
        // Market A goes: 100, 101, 99, 102, 98, 103, ... (alternating up/down)
        // Market B goes: 100, 99, 101, 98, 102, 97, ... (opposite pattern)
        var pricesA = new decimal[70];
        var pricesB = new decimal[70];
        pricesA[0] = 100m;
        pricesB[0] = 100m;

        for (var i = 1; i < 70; i++)
        {
            var change = (i % 2 == 0 ? -2m : 2m);
            pricesA[i] = pricesA[i - 1] + change;
            pricesB[i] = pricesB[i - 1] - change;
        }

        var marketCloses = new Dictionary<string, decimal[]>
        {
            ["MARKET_A"] = pricesA,
            ["MARKET_B"] = pricesB
        };

        var snapshot = CorrelationCalculator.ComputeMatrix(
            marketCloses,
            DateTime.UtcNow.Date);

        Assert.NotNull(snapshot);
        var matrix = CorrelationCalculator.ParseMatrix(snapshot.MatrixJson);
        Assert.True(matrix["MARKET_A|MARKET_B"] < -0.90m,
            $"Expected strong negative correlation but got {matrix["MARKET_A|MARKET_B"]}");
    }

    [Fact]
    public void ComputeMatrix_SetsSnapshotDateAndLookback()
    {
        var date = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var marketCloses = new Dictionary<string, decimal[]>
        {
            ["A"] = GenerateRisingPrices(100, 100m, 0.5m),
            ["B"] = GenerateRisingPrices(100, 200m, 0.3m)
        };

        var snapshot = CorrelationCalculator.ComputeMatrix(marketCloses, date, lookbackDays: 30);

        Assert.NotNull(snapshot);
        Assert.Equal(date, snapshot.SnapshotDate);
        Assert.Equal(30, snapshot.LookbackDays);
    }

    // ── ParseMatrix ──

    [Fact]
    public void ParseMatrix_ValidJson_ReturnsDictionary()
    {
        var json = """{"US_SP500|IN_NIFTY50":0.45,"US_SP500|UK_FTSE100":0.78}""";

        var matrix = CorrelationCalculator.ParseMatrix(json);

        Assert.Equal(2, matrix.Count);
        Assert.Equal(0.45m, matrix["US_SP500|IN_NIFTY50"]);
        Assert.Equal(0.78m, matrix["US_SP500|UK_FTSE100"]);
    }

    [Fact]
    public void ParseMatrix_EmptyOrNull_ReturnsEmptyDict()
    {
        Assert.Empty(CorrelationCalculator.ParseMatrix(null!));
        Assert.Empty(CorrelationCalculator.ParseMatrix(""));
        Assert.Empty(CorrelationCalculator.ParseMatrix("  "));
    }

    [Fact]
    public void ParseMatrix_InvalidJson_ReturnsEmptyDict()
    {
        Assert.Empty(CorrelationCalculator.ParseMatrix("not json"));
    }

    // ── DetectDecorrelations ──

    [Fact]
    public void DetectDecorrelations_SignificantDeviation_ReturnsEvent()
    {
        // Historical: correlation around 0.80 with low variance
        var historicalSnapshots = Enumerable.Range(0, 252).Select(i => new CorrelationSnapshot
        {
            SnapshotDate = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(i),
            LookbackDays = 60,
            MatrixJson = $$$"""{"US_SP500|IN_NIFTY50":{{{0.80m + (i % 2 == 0 ? 0.02m : -0.02m)}}}}"""
        }).ToList();

        // Current: correlation dropped to 0.30 (way below mean of ~0.80)
        var current = new CorrelationSnapshot
        {
            SnapshotDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            LookbackDays = 60,
            MatrixJson = """{"US_SP500|IN_NIFTY50":0.30}"""
        };

        var events = CorrelationCalculator.DetectDecorrelations(historicalSnapshots, current);

        Assert.Single(events);
        Assert.Equal("US_SP500", events[0].MarketA);
        Assert.Equal("IN_NIFTY50", events[0].MarketB);
        Assert.Equal(0.30m, events[0].CurrentCorrelation);
        Assert.True(events[0].ZScore < -1.0m, $"Expected z-score < -1 but got {events[0].ZScore}");
    }

    [Fact]
    public void DetectDecorrelations_WithinNormalRange_NoEvent()
    {
        var historicalSnapshots = Enumerable.Range(0, 100).Select(i => new CorrelationSnapshot
        {
            SnapshotDate = DateTime.UtcNow.AddDays(-100 + i),
            LookbackDays = 60,
            MatrixJson = $$$"""{"A|B":{{{0.50m + (i % 3 - 1) * 0.05m}}}}"""
        }).ToList();

        // Current: 0.52 — well within normal range
        var current = new CorrelationSnapshot
        {
            SnapshotDate = DateTime.UtcNow,
            LookbackDays = 60,
            MatrixJson = """{"A|B":0.52}"""
        };

        var events = CorrelationCalculator.DetectDecorrelations(historicalSnapshots, current);

        Assert.Empty(events);
    }

    [Fact]
    public void DetectDecorrelations_InsufficientHistory_ReturnsEmpty()
    {
        // Only 1 historical snapshot (need at least 2 for stddev)
        var historicalSnapshots = new List<CorrelationSnapshot>
        {
            new() { MatrixJson = """{"A|B":0.50}""" }
        };

        var current = new CorrelationSnapshot
        {
            MatrixJson = """{"A|B":0.10}"""
        };

        var events = CorrelationCalculator.DetectDecorrelations(historicalSnapshots, current);

        Assert.Empty(events);
    }

    [Fact]
    public void DetectDecorrelations_CustomSigmaThreshold()
    {
        // Historical: correlation around 0.60 with stddev ~0.05
        var historicalSnapshots = Enumerable.Range(0, 50).Select(i => new CorrelationSnapshot
        {
            SnapshotDate = DateTime.UtcNow.AddDays(-50 + i),
            LookbackDays = 60,
            MatrixJson = $$$"""{"A|B":{{{0.60m + (i % 2 == 0 ? 0.05m : -0.05m)}}}}"""
        }).ToList();

        // Current: 0.45 → ~3 stddevs below mean
        var current = new CorrelationSnapshot
        {
            SnapshotDate = DateTime.UtcNow,
            LookbackDays = 60,
            MatrixJson = """{"A|B":0.45}"""
        };

        // At 1σ → should trigger
        var events1 = CorrelationCalculator.DetectDecorrelations(historicalSnapshots, current, sigmaThreshold: 1.0m);
        Assert.Single(events1);

        // At 5σ → might not trigger (depends on actual deviation)
        var events5 = CorrelationCalculator.DetectDecorrelations(historicalSnapshots, current, sigmaThreshold: 5.0m);
        // Deviation is about 0.15 / 0.05 = 3σ, so 5σ should not trigger
        Assert.Empty(events5);
    }

    [Fact]
    public void DetectDecorrelations_PositiveDeviation_AlsoDetected()
    {
        // Historical: correlation around 0.30
        var historicalSnapshots = Enumerable.Range(0, 50).Select(i => new CorrelationSnapshot
        {
            SnapshotDate = DateTime.UtcNow.AddDays(-50 + i),
            LookbackDays = 60,
            MatrixJson = $$$"""{"A|B":{{{0.30m + (i % 2 == 0 ? 0.02m : -0.02m)}}}}"""
        }).ToList();

        // Current: correlation surged to 0.90 (way above mean)
        var current = new CorrelationSnapshot
        {
            SnapshotDate = DateTime.UtcNow,
            LookbackDays = 60,
            MatrixJson = """{"A|B":0.90}"""
        };

        var events = CorrelationCalculator.DetectDecorrelations(historicalSnapshots, current);

        Assert.Single(events);
        Assert.True(events[0].ZScore > 1.0m, "Should detect positive deviation too");
    }

    [Fact]
    public void DetectDecorrelations_MultiplePairs_IndependentDetection()
    {
        var historicalSnapshots = Enumerable.Range(0, 50).Select(i => new CorrelationSnapshot
        {
            SnapshotDate = DateTime.UtcNow.AddDays(-50 + i),
            LookbackDays = 60,
            MatrixJson = $$$"""{"A|B":{{{0.70m + (i % 2 == 0 ? 0.02m : -0.02m)}}},"A|C":{{{0.50m + (i % 2 == 0 ? 0.02m : -0.02m)}}}}"""
        }).ToList();

        // A|B deviated, A|C normal
        var current = new CorrelationSnapshot
        {
            SnapshotDate = DateTime.UtcNow,
            LookbackDays = 60,
            MatrixJson = """{"A|B":0.10,"A|C":0.51}"""
        };

        var events = CorrelationCalculator.DetectDecorrelations(historicalSnapshots, current);

        // Only A|B should trigger (dropped from ~0.70 to 0.10)
        Assert.Single(events);
        Assert.Equal("A", events[0].MarketA);
        Assert.Equal("B", events[0].MarketB);
    }

    // ── Helpers ──

    private static decimal[] GenerateRisingPrices(int count, decimal start, decimal step)
    {
        var prices = new decimal[count];
        for (var i = 0; i < count; i++)
            prices[i] = start + step * i;
        return prices;
    }
}
