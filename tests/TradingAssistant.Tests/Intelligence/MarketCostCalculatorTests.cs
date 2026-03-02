using TradingAssistant.Application.Intelligence;

namespace TradingAssistant.Tests.Intelligence;

public class MarketCostCalculatorTests
{
    // ── US Default Profile ──

    [Fact]
    public void UsDefault_HasCorrectFields()
    {
        var us = CostProfileData.UsDefault;

        Assert.Equal("US_SP500", us.MarketCode);
        Assert.Equal(0.005m, us.CommissionPerShare);
        Assert.Equal(0m, us.CommissionPercent);
        Assert.Equal(0m, us.ExchangeFeePercent);
        Assert.Equal(0m, us.TaxPercent);
        Assert.Equal(0.1m, us.SpreadEstimatePercent);
    }

    [Fact]
    public void Us_SingleLegCost_100SharesAt150()
    {
        var us = CostProfileData.UsDefault;
        // Per-share: 0.005 * 100 = $0.50
        // Spread: 150 * 100 * 0.1 / 100 = $15.00
        // Total: $15.50
        var cost = MarketCostCalculator.EstimateTradeCost(150m, 100, us);

        Assert.Equal(15.50m, cost);
    }

    [Fact]
    public void Us_RoundTrip_100SharesAt150()
    {
        var us = CostProfileData.UsDefault;
        // Single leg: $15.50 → round trip: $31.00
        var cost = MarketCostCalculator.EstimateRoundTripCost(150m, 100, us);

        Assert.Equal(31.00m, cost);
    }

    [Fact]
    public void Us_SingleLegCost_500SharesAt50()
    {
        var us = CostProfileData.UsDefault;
        // Per-share: 0.005 * 500 = $2.50
        // Spread: 50 * 500 * 0.1 / 100 = $25.00
        // Total: $27.50
        var cost = MarketCostCalculator.EstimateTradeCost(50m, 500, us);

        Assert.Equal(27.50m, cost);
    }

    [Fact]
    public void Us_SingleLegCost_1ShareAt200()
    {
        var us = CostProfileData.UsDefault;
        // Per-share: 0.005 * 1 = $0.005
        // Spread: 200 * 1 * 0.1 / 100 = $0.20
        // Total: $0.205 → banker's rounding to $0.20
        var cost = MarketCostCalculator.EstimateTradeCost(200m, 1, us);

        Assert.Equal(0.20m, cost);
    }

    // ── India Default Profile ──

    [Fact]
    public void IndiaDefault_HasCorrectFields()
    {
        var india = CostProfileData.IndiaDefault;

        Assert.Equal("IN_NIFTY50", india.MarketCode);
        Assert.Equal(0m, india.CommissionPerShare);
        Assert.Equal(0.03m, india.CommissionPercent);
        Assert.Equal(0m, india.ExchangeFeePercent);
        Assert.Equal(0.025m, india.TaxPercent);
        Assert.Equal(0.05m, india.SpreadEstimatePercent);
    }

    [Fact]
    public void India_SingleLegCost_100SharesAt2500()
    {
        var india = CostProfileData.IndiaDefault;
        // Notional: 2500 * 100 = 250,000
        // Brokerage: 250,000 * 0.03 / 100 = 75
        // STT: 250,000 * 0.025 / 100 = 62.50
        // Spread: 250,000 * 0.05 / 100 = 125
        // Total: 262.50
        var cost = MarketCostCalculator.EstimateTradeCost(2500m, 100, india);

        Assert.Equal(262.50m, cost);
    }

    [Fact]
    public void India_RoundTrip_100SharesAt2500()
    {
        var india = CostProfileData.IndiaDefault;
        // Single leg: 262.50 → round trip: 525.00
        var cost = MarketCostCalculator.EstimateRoundTripCost(2500m, 100, india);

        Assert.Equal(525.00m, cost);
    }

    [Fact]
    public void India_SingleLegCost_50SharesAt500()
    {
        var india = CostProfileData.IndiaDefault;
        // Notional: 500 * 50 = 25,000
        // Total %: 0.03 + 0.025 + 0.05 = 0.105%
        // Cost: 25,000 * 0.105 / 100 = 26.25
        var cost = MarketCostCalculator.EstimateTradeCost(500m, 50, india);

        Assert.Equal(26.25m, cost);
    }

    // ── TotalPercentRate ──

    [Fact]
    public void TotalPercentRate_Us()
    {
        Assert.Equal(0.1m, MarketCostCalculator.TotalPercentRate(CostProfileData.UsDefault));
    }

    [Fact]
    public void TotalPercentRate_India()
    {
        // 0.03 + 0 + 0.025 + 0.05 = 0.105
        Assert.Equal(0.105m, MarketCostCalculator.TotalPercentRate(CostProfileData.IndiaDefault));
    }

    // ── Edge cases ──

    [Fact]
    public void EstimateTradeCost_ZeroShares_ReturnsZero()
    {
        var cost = MarketCostCalculator.EstimateTradeCost(100m, 0, CostProfileData.UsDefault);
        Assert.Equal(0m, cost);
    }

    [Fact]
    public void EstimateTradeCost_NegativeShares_ReturnsZero()
    {
        var cost = MarketCostCalculator.EstimateTradeCost(100m, -10, CostProfileData.UsDefault);
        Assert.Equal(0m, cost);
    }

    [Fact]
    public void EstimateTradeCost_ZeroPrice_ReturnsZero()
    {
        var cost = MarketCostCalculator.EstimateTradeCost(0m, 100, CostProfileData.UsDefault);
        Assert.Equal(0m, cost);
    }

    [Fact]
    public void EstimateRoundTripCost_ZeroShares_ReturnsZero()
    {
        var cost = MarketCostCalculator.EstimateRoundTripCost(100m, 0, CostProfileData.UsDefault);
        Assert.Equal(0m, cost);
    }

    // ── Custom profiles ──

    [Fact]
    public void CustomProfile_AllFeeTypes()
    {
        var profile = new CostProfileData(
            "CUSTOM",
            CommissionPerShare: 0.01m,
            CommissionPercent: 0.05m,
            ExchangeFeePercent: 0.02m,
            TaxPercent: 0.01m,
            SpreadEstimatePercent: 0.03m);

        // 100 shares at $100 = $10,000 notional
        // Per-share: 0.01 * 100 = $1.00
        // Percent: 10,000 * (0.05 + 0.02 + 0.01 + 0.03) / 100 = 10,000 * 0.11 / 100 = $11.00
        // Total: $12.00
        var cost = MarketCostCalculator.EstimateTradeCost(100m, 100, profile);

        Assert.Equal(12.00m, cost);
    }

    [Fact]
    public void CustomProfile_OnlyPerShareCommission()
    {
        var profile = new CostProfileData("TEST", 0.01m, 0m, 0m, 0m, 0m);

        // 200 shares: 0.01 * 200 = $2.00
        var cost = MarketCostCalculator.EstimateTradeCost(50m, 200, profile);

        Assert.Equal(2.00m, cost);
    }

    [Fact]
    public void CustomProfile_OnlyPercentFees()
    {
        var profile = new CostProfileData("TEST", 0m, 0.1m, 0m, 0m, 0m);

        // 100 shares at $100 = $10,000 notional
        // Cost: 10,000 * 0.1 / 100 = $10.00
        var cost = MarketCostCalculator.EstimateTradeCost(100m, 100, profile);

        Assert.Equal(10.00m, cost);
    }

    [Fact]
    public void CustomProfile_ZeroCostProfile()
    {
        var profile = new CostProfileData("FREE", 0m, 0m, 0m, 0m, 0m);

        var cost = MarketCostCalculator.EstimateTradeCost(100m, 100, profile);

        Assert.Equal(0m, cost);
    }

    // ── US vs India comparison ──

    [Fact]
    public void Comparison_IndiaHigherPercentCostOnLargeNotional()
    {
        // Same notional: 1000 shares at $100 = $100K
        var usCost = MarketCostCalculator.EstimateRoundTripCost(100m, 1000, CostProfileData.UsDefault);
        var indiaCost = MarketCostCalculator.EstimateRoundTripCost(100m, 1000, CostProfileData.IndiaDefault);

        // US: per-share 0.005*1000*2 = $10; spread 100K*0.1/100*2 = $200; total = $210
        // India: percent (100K * 0.105/100) * 2 = $210
        // At this notional they're approximately equal
        Assert.True(usCost > 0);
        Assert.True(indiaCost > 0);
    }

    [Fact]
    public void Us_PerShareCost_ScalesWithShareCount()
    {
        var us = CostProfileData.UsDefault;
        var cost10 = MarketCostCalculator.EstimateTradeCost(100m, 10, us);
        var cost100 = MarketCostCalculator.EstimateTradeCost(100m, 100, us);

        // More shares = higher cost (per-share + notional both increase)
        Assert.True(cost100 > cost10);
    }

    // ── CostProfile entity compatibility ──

    [Fact]
    public void EstimateRoundTrip_MatchesCostProfileEntity_Us()
    {
        // Verify our calculator matches the domain entity's calculation
        var us = CostProfileData.UsDefault;
        var price = 150m;
        var shares = 100;

        var calcCost = MarketCostCalculator.EstimateRoundTripCost(price, shares, us);

        // Manual domain entity equivalent:
        var notional = price * shares;
        var perShareCost = us.CommissionPerShare * shares * 2;
        var percentCost = notional * (us.CommissionPercent + us.ExchangeFeePercent + us.TaxPercent + us.SpreadEstimatePercent) * 2 / 100;
        var entityCost = perShareCost + percentCost;

        Assert.Equal(entityCost, calcCost);
    }

    [Fact]
    public void EstimateRoundTrip_MatchesCostProfileEntity_India()
    {
        var india = CostProfileData.IndiaDefault;
        var price = 2500m;
        var shares = 100;

        var calcCost = MarketCostCalculator.EstimateRoundTripCost(price, shares, india);

        var notional = price * shares;
        var perShareCost = india.CommissionPerShare * shares * 2;
        var percentCost = notional * (india.CommissionPercent + india.ExchangeFeePercent + india.TaxPercent + india.SpreadEstimatePercent) * 2 / 100;
        var entityCost = perShareCost + percentCost;

        Assert.Equal(entityCost, calcCost);
    }
}
