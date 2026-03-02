using TradingAssistant.Application.Intelligence;

namespace TradingAssistant.Tests.Intelligence;

public class GeographicRiskBudgetTests
{
    // ── Check: Basic pass/block ──

    [Fact]
    public void Check_UnderLimit_Allowed()
    {
        var marketNotionals = new Dictionary<string, decimal>
        {
            ["US_SP500"] = 20_000m // 20% of 100K
        };

        var result = GeographicRiskBudget.Check(
            "US_SP500", 10_000m, marketNotionals, 100_000m, 50m);

        Assert.True(result.Allowed);
        Assert.Equal(20m, result.CurrentAllocationPercent);
        Assert.Equal(30m, result.ProposedAllocationPercent);
        Assert.Contains("PASS", result.Detail);
    }

    [Fact]
    public void Check_ExceedsLimit_Blocked()
    {
        var marketNotionals = new Dictionary<string, decimal>
        {
            ["US_SP500"] = 45_000m // 45% of 100K
        };

        var result = GeographicRiskBudget.Check(
            "US_SP500", 10_000m, marketNotionals, 100_000m, 50m);

        Assert.False(result.Allowed);
        Assert.Equal(45m, result.CurrentAllocationPercent);
        Assert.Equal(55m, result.ProposedAllocationPercent);
        Assert.Contains("BLOCKED", result.Detail);
    }

    [Fact]
    public void Check_ExactlyAtLimit_Allowed()
    {
        // 30% existing + 20% proposed = exactly 50% → allowed (not exceeded)
        var marketNotionals = new Dictionary<string, decimal>
        {
            ["US_SP500"] = 30_000m
        };

        var result = GeographicRiskBudget.Check(
            "US_SP500", 20_000m, marketNotionals, 100_000m, 50m);

        Assert.True(result.Allowed);
        Assert.Equal(50m, result.ProposedAllocationPercent);
    }

    [Fact]
    public void Check_JustOverLimit_Blocked()
    {
        var marketNotionals = new Dictionary<string, decimal>
        {
            ["US_SP500"] = 30_000m
        };

        // 30% + 20.01% = 50.01% → exceeds 50%
        var result = GeographicRiskBudget.Check(
            "US_SP500", 20_010m, marketNotionals, 100_000m, 50m);

        Assert.False(result.Allowed);
    }

    // ── Check: Default threshold ──

    [Fact]
    public void Check_DefaultThreshold_Is50Percent()
    {
        Assert.Equal(50m, GeographicRiskBudget.DefaultMaxAllocationPercent);
    }

    [Fact]
    public void Check_UsesDefaultThreshold()
    {
        var marketNotionals = new Dictionary<string, decimal>
        {
            ["US_SP500"] = 40_000m
        };

        // 40% + 15% = 55% > default 50% → blocked
        var result = GeographicRiskBudget.Check(
            "US_SP500", 15_000m, marketNotionals, 100_000m);

        Assert.False(result.Allowed);
        Assert.Equal(50m, result.MaxAllocationPercent);
    }

    // ── Check: Custom thresholds ──

    [Fact]
    public void Check_CustomThreshold_30Percent()
    {
        var marketNotionals = new Dictionary<string, decimal>
        {
            ["IN_NIFTY50"] = 25_000m
        };

        // 25% + 10% = 35% > 30% → blocked
        var result = GeographicRiskBudget.Check(
            "IN_NIFTY50", 10_000m, marketNotionals, 100_000m, 30m);

        Assert.False(result.Allowed);
    }

    [Fact]
    public void Check_CustomThreshold_80Percent()
    {
        var marketNotionals = new Dictionary<string, decimal>
        {
            ["US_SP500"] = 70_000m
        };

        // 70% + 8% = 78% < 80% → allowed
        var result = GeographicRiskBudget.Check(
            "US_SP500", 8_000m, marketNotionals, 100_000m, 80m);

        Assert.True(result.Allowed);
    }

    // ── Check: Multiple markets ──

    [Fact]
    public void Check_MultipleMarkets_IndependentBudgets()
    {
        var marketNotionals = new Dictionary<string, decimal>
        {
            ["US_SP500"] = 45_000m,
            ["IN_NIFTY50"] = 30_000m
        };

        // US at 45%, adding to India (30% + 10% = 40%) → allowed
        var result = GeographicRiskBudget.Check(
            "IN_NIFTY50", 10_000m, marketNotionals, 100_000m, 50m);

        Assert.True(result.Allowed);
        Assert.Equal(30m, result.CurrentAllocationPercent);
        Assert.Equal(40m, result.ProposedAllocationPercent);
    }

    [Fact]
    public void Check_MultipleMarkets_OneExceedsLimit()
    {
        var marketNotionals = new Dictionary<string, decimal>
        {
            ["US_SP500"] = 45_000m,
            ["IN_NIFTY50"] = 30_000m
        };

        // Adding more to US: 45% + 10% = 55% → blocked
        var result = GeographicRiskBudget.Check(
            "US_SP500", 10_000m, marketNotionals, 100_000m, 50m);

        Assert.False(result.Allowed);
    }

    // ── Check: New market (no existing exposure) ──

    [Fact]
    public void Check_NewMarket_NoExistingExposure_Allowed()
    {
        var marketNotionals = new Dictionary<string, decimal>
        {
            ["US_SP500"] = 40_000m
        };

        // New market, 0% + 20% = 20% → allowed
        var result = GeographicRiskBudget.Check(
            "EU_STOXX50", 20_000m, marketNotionals, 100_000m, 50m);

        Assert.True(result.Allowed);
        Assert.Equal(0m, result.CurrentAllocationPercent);
        Assert.Equal(20m, result.ProposedAllocationPercent);
    }

    // ── Check: Edge cases ──

    [Fact]
    public void Check_ZeroEquity_ReturnsAllowed()
    {
        var marketNotionals = new Dictionary<string, decimal>();

        var result = GeographicRiskBudget.Check(
            "US_SP500", 10_000m, marketNotionals, 0m, 50m);

        Assert.True(result.Allowed);
        Assert.Contains("no equity", result.Detail);
    }

    [Fact]
    public void Check_NegativeEquity_ReturnsAllowed()
    {
        var marketNotionals = new Dictionary<string, decimal>();

        var result = GeographicRiskBudget.Check(
            "US_SP500", 10_000m, marketNotionals, -5_000m, 50m);

        Assert.True(result.Allowed);
    }

    [Fact]
    public void Check_EmptyPortfolio_Allowed()
    {
        var marketNotionals = new Dictionary<string, decimal>();

        // Empty portfolio, adding 30% → allowed at 50% limit
        var result = GeographicRiskBudget.Check(
            "US_SP500", 30_000m, marketNotionals, 100_000m, 50m);

        Assert.True(result.Allowed);
        Assert.Equal(0m, result.CurrentAllocationPercent);
        Assert.Equal(30m, result.ProposedAllocationPercent);
    }

    [Fact]
    public void Check_100PercentAllocation_ThresholdExceeded()
    {
        var marketNotionals = new Dictionary<string, decimal>();

        // 100% in one market → exceeds 50% limit
        var result = GeographicRiskBudget.Check(
            "US_SP500", 100_000m, marketNotionals, 100_000m, 50m);

        Assert.False(result.Allowed);
        Assert.Equal(100m, result.ProposedAllocationPercent);
    }

    [Fact]
    public void Check_ZeroProposedNotional_Allowed()
    {
        var marketNotionals = new Dictionary<string, decimal>
        {
            ["US_SP500"] = 45_000m
        };

        var result = GeographicRiskBudget.Check(
            "US_SP500", 0m, marketNotionals, 100_000m, 50m);

        Assert.True(result.Allowed);
        Assert.Equal(45m, result.ProposedAllocationPercent);
    }

    // ── Check: Result fields ──

    [Fact]
    public void Check_ResultFields_ArePopulated()
    {
        var marketNotionals = new Dictionary<string, decimal>
        {
            ["US_SP500"] = 30_000m
        };

        var result = GeographicRiskBudget.Check(
            "US_SP500", 10_000m, marketNotionals, 100_000m, 50m);

        Assert.Equal("US_SP500", result.CandidateMarket);
        Assert.Equal(30_000m, result.CurrentMarketNotional);
        Assert.Equal(10_000m, result.ProposedNotional);
        Assert.Equal(100_000m, result.TotalEquity);
        Assert.Equal(50m, result.MaxAllocationPercent);
    }

    // ── ComputeMarketNotionals ──

    [Fact]
    public void ComputeMarketNotionals_GroupsByMarket()
    {
        var positions = new List<(string Symbol, decimal Notional)>
        {
            ("AAPL", 15_000m),
            ("MSFT", 10_000m),
            ("RELIANCE.NS", 8_000m),
            ("TCS.NS", 12_000m)
        };
        var mapping = new Dictionary<string, string>
        {
            ["AAPL"] = "US_SP500",
            ["MSFT"] = "US_SP500",
            ["RELIANCE.NS"] = "IN_NIFTY50",
            ["TCS.NS"] = "IN_NIFTY50"
        };

        var result = GeographicRiskBudget.ComputeMarketNotionals(positions, mapping);

        Assert.Equal(25_000m, result["US_SP500"]);
        Assert.Equal(20_000m, result["IN_NIFTY50"]);
    }

    [Fact]
    public void ComputeMarketNotionals_EmptyPositions_EmptyResult()
    {
        var positions = new List<(string Symbol, decimal Notional)>();
        var mapping = new Dictionary<string, string>();

        var result = GeographicRiskBudget.ComputeMarketNotionals(positions, mapping);

        Assert.Empty(result);
    }

    [Fact]
    public void ComputeMarketNotionals_UnmappedSymbols_Ignored()
    {
        var positions = new List<(string Symbol, decimal Notional)>
        {
            ("AAPL", 15_000m),
            ("UNKNOWN", 5_000m)
        };
        var mapping = new Dictionary<string, string>
        {
            ["AAPL"] = "US_SP500"
        };

        var result = GeographicRiskBudget.ComputeMarketNotionals(positions, mapping);

        Assert.Single(result);
        Assert.Equal(15_000m, result["US_SP500"]);
    }

    [Fact]
    public void ComputeMarketNotionals_SingleMarket()
    {
        var positions = new List<(string Symbol, decimal Notional)>
        {
            ("AAPL", 15_000m),
            ("MSFT", 10_000m),
            ("GOOG", 5_000m)
        };
        var mapping = new Dictionary<string, string>
        {
            ["AAPL"] = "US_SP500",
            ["MSFT"] = "US_SP500",
            ["GOOG"] = "US_SP500"
        };

        var result = GeographicRiskBudget.ComputeMarketNotionals(positions, mapping);

        Assert.Single(result);
        Assert.Equal(30_000m, result["US_SP500"]);
    }

    // ── Integration: Known scenarios ──

    [Fact]
    public void KnownScenario_DiversifiedPortfolio_AllowsNewMarket()
    {
        // Portfolio: 40% US, 35% India
        var marketNotionals = new Dictionary<string, decimal>
        {
            ["US_SP500"] = 40_000m,
            ["IN_NIFTY50"] = 35_000m
        };

        // Adding new EU position: 0% + 15% = 15% → allowed
        var result = GeographicRiskBudget.Check(
            "EU_STOXX50", 15_000m, marketNotionals, 100_000m, 50m);

        Assert.True(result.Allowed);
    }

    [Fact]
    public void KnownScenario_ConcentratedPortfolio_BlocksFurther()
    {
        // Already 48% in US
        var marketNotionals = new Dictionary<string, decimal>
        {
            ["US_SP500"] = 48_000m,
            ["IN_NIFTY50"] = 20_000m
        };

        // Trying to add another 5% US → 53% > 50% → blocked
        var result = GeographicRiskBudget.Check(
            "US_SP500", 5_000m, marketNotionals, 100_000m, 50m);

        Assert.False(result.Allowed);
        Assert.Contains("US_SP500", result.Detail);
    }

    [Theory]
    [InlineData(50, true)]   // 40% + 10% = 50% → at limit, allowed
    [InlineData(40, false)]  // 40% + 10% = 50% > 40% → blocked
    [InlineData(60, true)]   // 40% + 10% = 50% < 60% → allowed
    public void Check_VariousThresholds(decimal threshold, bool expectedAllowed)
    {
        var marketNotionals = new Dictionary<string, decimal>
        {
            ["US_SP500"] = 40_000m
        };

        var result = GeographicRiskBudget.Check(
            "US_SP500", 10_000m, marketNotionals, 100_000m, threshold);

        Assert.Equal(expectedAllowed, result.Allowed);
    }
}
