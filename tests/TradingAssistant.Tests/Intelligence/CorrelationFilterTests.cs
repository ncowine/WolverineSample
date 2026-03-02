using TradingAssistant.Application.Intelligence;

namespace TradingAssistant.Tests.Intelligence;

public class CorrelationFilterTests
{
    // ── MakeKey ──

    [Fact]
    public void MakeKey_AlphabeticalOrder()
    {
        Assert.Equal("AAPL|MSFT", CorrelationFilter.MakeKey("AAPL", "MSFT"));
        Assert.Equal("AAPL|MSFT", CorrelationFilter.MakeKey("MSFT", "AAPL"));
    }

    [Fact]
    public void MakeKey_SameSymbol()
    {
        Assert.Equal("AAPL|AAPL", CorrelationFilter.MakeKey("AAPL", "AAPL"));
    }

    // ── Check: Pass scenarios ──

    [Fact]
    public void Check_NoOpenPositions_Pass()
    {
        var result = CorrelationFilter.Check(
            "AAPL",
            new List<string>(),
            new Dictionary<string, decimal>());

        Assert.Equal(CorrelationAction.Pass, result.Action);
        Assert.Equal(1m, result.SizeMultiplier);
        Assert.Equal(0, result.PositionsChecked);
    }

    [Fact]
    public void Check_LowCorrelation_Pass()
    {
        var correlations = new Dictionary<string, decimal>
        {
            ["AAPL|MSFT"] = 0.3m,
            ["AAPL|GOOG"] = 0.2m
        };

        var result = CorrelationFilter.Check(
            "AAPL",
            new List<string> { "MSFT", "GOOG" },
            correlations);

        Assert.Equal(CorrelationAction.Pass, result.Action);
        Assert.Equal(1m, result.SizeMultiplier);
        Assert.Equal(0.25m, result.AvgCorrelation); // (0.3 + 0.2) / 2
        Assert.Equal(2, result.PositionsChecked);
    }

    [Fact]
    public void Check_ZeroCorrelation_Pass()
    {
        var correlations = new Dictionary<string, decimal>
        {
            ["AAPL|GLD"] = 0.0m
        };

        var result = CorrelationFilter.Check(
            "AAPL",
            new List<string> { "GLD" },
            correlations);

        Assert.Equal(CorrelationAction.Pass, result.Action);
        Assert.Equal(1m, result.SizeMultiplier);
    }

    [Fact]
    public void Check_NoCorrelationData_Pass()
    {
        // No correlation data for the pair → default to pass
        var result = CorrelationFilter.Check(
            "AAPL",
            new List<string> { "XYZ" },
            new Dictionary<string, decimal>());

        Assert.Equal(CorrelationAction.Pass, result.Action);
        Assert.Equal(1m, result.SizeMultiplier);
    }

    // ── Check: Block scenarios ──

    [Fact]
    public void Check_HighCorrelation_Block()
    {
        var correlations = new Dictionary<string, decimal>
        {
            ["AAPL|MSFT"] = 0.85m
        };

        var result = CorrelationFilter.Check(
            "AAPL",
            new List<string> { "MSFT" },
            correlations);

        Assert.Equal(CorrelationAction.Block, result.Action);
        Assert.Equal(0m, result.SizeMultiplier);
        Assert.Equal(0.85m, result.AvgCorrelation);
    }

    [Fact]
    public void Check_AvgAboveBlock_Block()
    {
        // One high, one medium → avg above threshold
        var correlations = new Dictionary<string, decimal>
        {
            ["AAPL|MSFT"] = 0.9m,
            ["AAPL|GOOG"] = 0.6m
        };

        var result = CorrelationFilter.Check(
            "AAPL",
            new List<string> { "MSFT", "GOOG" },
            correlations);

        // Avg = (0.9 + 0.6) / 2 = 0.75 > 0.7
        Assert.Equal(CorrelationAction.Block, result.Action);
        Assert.Equal(0.75m, result.AvgCorrelation);
    }

    [Fact]
    public void Check_NegativeHighCorrelation_Block()
    {
        // Strong negative correlation is also concentrated risk
        // (both sides of a pair trade, or inverse positions)
        var correlations = new Dictionary<string, decimal>
        {
            ["AAPL|SH"] = -0.85m // Inverse ETF
        };

        var result = CorrelationFilter.Check(
            "AAPL",
            new List<string> { "SH" },
            correlations);

        // Uses absolute correlation: |-0.85| = 0.85 > 0.7
        Assert.Equal(CorrelationAction.Block, result.Action);
    }

    // ── Check: Reduce scenarios ──

    [Fact]
    public void Check_MediumCorrelation_Reduce()
    {
        var correlations = new Dictionary<string, decimal>
        {
            ["AAPL|MSFT"] = 0.6m
        };

        var result = CorrelationFilter.Check(
            "AAPL",
            new List<string> { "MSFT" },
            correlations);

        Assert.Equal(CorrelationAction.Reduce, result.Action);
        // Linear: at 0.5 → 1.0, at 0.7 → 0.0
        // At 0.6: 1.0 - (0.6 - 0.5) / (0.7 - 0.5) = 1.0 - 0.5 = 0.5
        Assert.Equal(0.5m, result.SizeMultiplier);
    }

    [Fact]
    public void Check_AtReduceThreshold_FullSize()
    {
        var correlations = new Dictionary<string, decimal>
        {
            ["AAPL|MSFT"] = 0.5m
        };

        var result = CorrelationFilter.Check(
            "AAPL",
            new List<string> { "MSFT" },
            correlations);

        Assert.Equal(CorrelationAction.Reduce, result.Action);
        // At exactly 0.5: multiplier = 1.0 - (0.5 - 0.5) / (0.7 - 0.5) = 1.0
        Assert.Equal(1m, result.SizeMultiplier);
    }

    [Fact]
    public void Check_JustBelowBlock_SmallSize()
    {
        var correlations = new Dictionary<string, decimal>
        {
            ["AAPL|MSFT"] = 0.69m
        };

        var result = CorrelationFilter.Check(
            "AAPL",
            new List<string> { "MSFT" },
            correlations);

        Assert.Equal(CorrelationAction.Reduce, result.Action);
        // At 0.69: 1.0 - (0.69 - 0.5) / 0.2 = 1.0 - 0.95 = 0.05
        Assert.Equal(0.05m, result.SizeMultiplier);
    }

    [Fact]
    public void Check_AtBlockThreshold_Block()
    {
        var correlations = new Dictionary<string, decimal>
        {
            ["AAPL|MSFT"] = 0.71m
        };

        var result = CorrelationFilter.Check(
            "AAPL",
            new List<string> { "MSFT" },
            correlations);

        Assert.Equal(CorrelationAction.Block, result.Action);
    }

    // ── Check: Custom thresholds ──

    [Fact]
    public void Check_CustomThresholds()
    {
        var correlations = new Dictionary<string, decimal>
        {
            ["AAPL|MSFT"] = 0.55m
        };

        // With tighter thresholds: reduce at 0.4, block at 0.6
        var result = CorrelationFilter.Check(
            "AAPL",
            new List<string> { "MSFT" },
            correlations,
            blockThreshold: 0.6m,
            reduceThreshold: 0.4m);

        Assert.Equal(CorrelationAction.Reduce, result.Action);
        // At 0.55 with range [0.4, 0.6]: 1.0 - (0.55 - 0.4) / 0.2 = 1.0 - 0.75 = 0.25
        Assert.Equal(0.25m, result.SizeMultiplier);
    }

    [Fact]
    public void Check_CustomThresholds_Block()
    {
        var correlations = new Dictionary<string, decimal>
        {
            ["AAPL|MSFT"] = 0.65m
        };

        var result = CorrelationFilter.Check(
            "AAPL",
            new List<string> { "MSFT" },
            correlations,
            blockThreshold: 0.6m,
            reduceThreshold: 0.4m);

        Assert.Equal(CorrelationAction.Block, result.Action);
    }

    // ── Check: Multiple positions ──

    [Fact]
    public void Check_MultiplePositions_AvgDeterminesAction()
    {
        var correlations = new Dictionary<string, decimal>
        {
            ["AAPL|MSFT"] = 0.8m,   // High with MSFT
            ["AAPL|GLD"] = 0.1m,     // Low with GLD
            ["AAPL|TLT"] = 0.05m     // Low with TLT
        };

        var result = CorrelationFilter.Check(
            "AAPL",
            new List<string> { "MSFT", "GLD", "TLT" },
            correlations);

        // Avg = (0.8 + 0.1 + 0.05) / 3 ≈ 0.3167 → Pass
        Assert.Equal(CorrelationAction.Pass, result.Action);
        Assert.Equal(3, result.PositionsChecked);
    }

    [Fact]
    public void Check_MultipleHighlyCorrelatedPositions_Block()
    {
        var correlations = new Dictionary<string, decimal>
        {
            ["AAPL|MSFT"] = 0.8m,
            ["AAPL|GOOG"] = 0.75m,
            ["AAPL|META"] = 0.72m
        };

        var result = CorrelationFilter.Check(
            "AAPL",
            new List<string> { "MSFT", "GOOG", "META" },
            correlations);

        // Avg = (0.8 + 0.75 + 0.72) / 3 ≈ 0.7567 > 0.7 → Block
        Assert.Equal(CorrelationAction.Block, result.Action);
    }

    [Fact]
    public void Check_PartialCorrelationData_UsesAvailableOnly()
    {
        // Only have data for AAPL|MSFT, not AAPL|XYZ
        var correlations = new Dictionary<string, decimal>
        {
            ["AAPL|MSFT"] = 0.3m
        };

        var result = CorrelationFilter.Check(
            "AAPL",
            new List<string> { "MSFT", "XYZ" },
            correlations);

        // Only 1 correlation found, avg = 0.3 → Pass
        Assert.Equal(CorrelationAction.Pass, result.Action);
        Assert.Equal(1, result.PositionsChecked);
        Assert.Equal(0.3m, result.AvgCorrelation);
    }

    // ── AdjustShares ──

    [Fact]
    public void AdjustShares_Block_ReturnsZero()
    {
        var result = new CorrelationCheckResult(
            CorrelationAction.Block, 0.85m, 0.85m, 0m, 1, "Blocked");

        Assert.Equal(0, CorrelationFilter.AdjustShares(100, result));
    }

    [Fact]
    public void AdjustShares_Reduce_ScalesDown()
    {
        var result = new CorrelationCheckResult(
            CorrelationAction.Reduce, 0.6m, 0.6m, 0.5m, 1, "Reduced");

        Assert.Equal(50, CorrelationFilter.AdjustShares(100, result));
    }

    [Fact]
    public void AdjustShares_Pass_FullSize()
    {
        var result = new CorrelationCheckResult(
            CorrelationAction.Pass, 0.2m, 0.2m, 1m, 1, "Passed");

        Assert.Equal(100, CorrelationFilter.AdjustShares(100, result));
    }

    [Fact]
    public void AdjustShares_Reduce_FloorsToInt()
    {
        // 100 shares × 0.3 multiplier = 30 shares
        var result = new CorrelationCheckResult(
            CorrelationAction.Reduce, 0.64m, 0.64m, 0.3m, 1, "Reduced");

        Assert.Equal(30, CorrelationFilter.AdjustShares(100, result));
    }

    [Fact]
    public void AdjustShares_SmallMultiplier_MayYieldZero()
    {
        // 5 shares × 0.05 = 0.25 → floored to 0
        var result = new CorrelationCheckResult(
            CorrelationAction.Reduce, 0.69m, 0.69m, 0.05m, 1, "Reduced");

        Assert.Equal(0, CorrelationFilter.AdjustShares(5, result));
    }

    // ── Integration: CorrelationCalculator → CorrelationFilter ──

    [Fact]
    public void Integration_ParsedMatrixWorksWithFilter()
    {
        // Simulate a correlation matrix JSON from CorrelationCalculator
        var matrixJson = """{"AAPL|MSFT":0.82,"AAPL|GOOG":0.65,"GOOG|MSFT":0.71}""";
        var parsed = CorrelationCalculator.ParseMatrix(matrixJson);

        // Check AAPL vs [MSFT, GOOG] portfolio
        var result = CorrelationFilter.Check(
            "AAPL",
            new List<string> { "MSFT", "GOOG" },
            parsed);

        // Avg = (0.82 + 0.65) / 2 = 0.735 > 0.7 → Block
        Assert.Equal(CorrelationAction.Block, result.Action);
        Assert.Equal(0.735m, result.AvgCorrelation);
    }

    [Fact]
    public void Integration_ParsedMatrix_ReduceScenario()
    {
        var matrixJson = """{"AAPL|GOOG":0.55,"AAPL|MSFT":0.60}""";
        var parsed = CorrelationCalculator.ParseMatrix(matrixJson);

        var result = CorrelationFilter.Check(
            "AAPL",
            new List<string> { "GOOG", "MSFT" },
            parsed);

        // Avg = (0.55 + 0.60) / 2 = 0.575 → Reduce
        Assert.Equal(CorrelationAction.Reduce, result.Action);
        // Multiplier: 1.0 - (0.575 - 0.5) / 0.2 = 1.0 - 0.375 = 0.625
        Assert.Equal(0.625m, result.SizeMultiplier);
    }

    // ── Edge cases ──

    [Fact]
    public void Check_SinglePosition_SingleCorrelation()
    {
        var correlations = new Dictionary<string, decimal>
        {
            ["AAPL|MSFT"] = 0.55m
        };

        var result = CorrelationFilter.Check(
            "AAPL",
            new List<string> { "MSFT" },
            correlations);

        Assert.Equal(CorrelationAction.Reduce, result.Action);
        Assert.Equal(1, result.PositionsChecked);
    }

    [Fact]
    public void Check_ExactlyAtBlockThreshold_Block()
    {
        // > 0.7, not >= 0.7, so 0.7 should be reduce, 0.7001 should block
        var correlations = new Dictionary<string, decimal>
        {
            ["AAPL|MSFT"] = 0.7001m
        };

        var result = CorrelationFilter.Check(
            "AAPL",
            new List<string> { "MSFT" },
            correlations);

        Assert.Equal(CorrelationAction.Block, result.Action);
    }

    [Fact]
    public void Check_ExactlyAtReduceThreshold_Reduce()
    {
        var correlations = new Dictionary<string, decimal>
        {
            ["AAPL|MSFT"] = 0.5m
        };

        var result = CorrelationFilter.Check(
            "AAPL",
            new List<string> { "MSFT" },
            correlations);

        // >= reduceThreshold → Reduce (but multiplier = 1.0)
        Assert.Equal(CorrelationAction.Reduce, result.Action);
        Assert.Equal(1m, result.SizeMultiplier);
    }

    [Fact]
    public void Check_JustBelowReduceThreshold_Pass()
    {
        var correlations = new Dictionary<string, decimal>
        {
            ["AAPL|MSFT"] = 0.49m
        };

        var result = CorrelationFilter.Check(
            "AAPL",
            new List<string> { "MSFT" },
            correlations);

        Assert.Equal(CorrelationAction.Pass, result.Action);
    }
}
