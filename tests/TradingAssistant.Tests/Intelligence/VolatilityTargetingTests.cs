using TradingAssistant.Application.Intelligence;

namespace TradingAssistant.Tests.Intelligence;

public class VolatilityTargetingTests
{
    // ── CalculateShares ──

    [Fact]
    public void CalculateShares_BasicFormula()
    {
        // Risk $1000, ATR $5, multiplier 2 → riskPerShare = $10 → 100 shares
        var shares = VolatilityTargeting.CalculateShares(1000m, 5m, 2m);
        Assert.Equal(100, shares);
    }

    [Fact]
    public void CalculateShares_LowVolatility_MoreShares()
    {
        // Low ATR = calm market → more shares
        // Risk $1000, ATR $2, multiplier 2 → riskPerShare = $4 → 250 shares
        var shares = VolatilityTargeting.CalculateShares(1000m, 2m, 2m);
        Assert.Equal(250, shares);
    }

    [Fact]
    public void CalculateShares_HighVolatility_FewerShares()
    {
        // High ATR = volatile market → fewer shares
        // Risk $1000, ATR $20, multiplier 2 → riskPerShare = $40 → 25 shares
        var shares = VolatilityTargeting.CalculateShares(1000m, 20m, 2m);
        Assert.Equal(25, shares);
    }

    [Fact]
    public void CalculateShares_VeryHighAtr_ReturnsZero()
    {
        // ATR so high that riskPerShare > targetRisk → 0 shares
        var shares = VolatilityTargeting.CalculateShares(100m, 200m, 2m);
        Assert.Equal(0, shares);
    }

    [Fact]
    public void CalculateShares_ZeroAtr_ReturnsZero()
    {
        var shares = VolatilityTargeting.CalculateShares(1000m, 0m, 2m);
        Assert.Equal(0, shares);
    }

    [Fact]
    public void CalculateShares_ZeroRisk_ReturnsZero()
    {
        var shares = VolatilityTargeting.CalculateShares(0m, 5m, 2m);
        Assert.Equal(0, shares);
    }

    [Fact]
    public void CalculateShares_NegativeInputs_ReturnsZero()
    {
        Assert.Equal(0, VolatilityTargeting.CalculateShares(-100m, 5m, 2m));
        Assert.Equal(0, VolatilityTargeting.CalculateShares(100m, -5m, 2m));
        Assert.Equal(0, VolatilityTargeting.CalculateShares(100m, 5m, -2m));
    }

    [Fact]
    public void CalculateShares_CustomMultiplier()
    {
        // Risk $1000, ATR $5, multiplier 1.5 → riskPerShare = $7.50 → 133 shares
        var shares = VolatilityTargeting.CalculateShares(1000m, 5m, 1.5m);
        Assert.Equal(133, shares);
    }

    [Fact]
    public void CalculateShares_DefaultMultiplier()
    {
        // Risk $1000, ATR $5, default multiplier (2) → riskPerShare = $10 → 100 shares
        var shares = VolatilityTargeting.CalculateShares(1000m, 5m);
        Assert.Equal(100, shares);
    }

    [Fact]
    public void CalculateShares_FractionalResult_FloorsToInt()
    {
        // Risk $1000, ATR $3, multiplier 2 → riskPerShare = $6 → 166.67 → 166 shares
        var shares = VolatilityTargeting.CalculateShares(1000m, 3m, 2m);
        Assert.Equal(166, shares);
    }

    // ── CalculatePositionSize ──

    [Fact]
    public void PositionSize_BasicScenario()
    {
        // Equity $100K, risk 1%, price $50, ATR $2, multiplier 2
        // targetRisk = $1000, riskPerShare = $4, shares = 250
        var result = VolatilityTargeting.CalculatePositionSize(
            equity: 100_000m, riskPercent: 1m, price: 50m, atr: 2m,
            availableCash: 100_000m);

        Assert.Equal("VolTarget", result.Method);
        Assert.Equal(250, result.Shares);
        Assert.Equal(1000m, result.TargetRiskDollars);
        Assert.Equal(2m, result.AtrUsed);
        Assert.Equal(4m, result.RiskPerShare); // ATR × multiplier
    }

    [Fact]
    public void PositionSize_HighRiskPercent_MoreShares()
    {
        // Equity $100K, risk 2%, ATR $2, multiplier 2
        // targetRisk = $2000, riskPerShare = $4, shares = 500
        var result = VolatilityTargeting.CalculatePositionSize(
            equity: 100_000m, riskPercent: 2m, price: 50m, atr: 2m,
            availableCash: 100_000m);

        Assert.Equal(500, result.Shares);
        Assert.Equal(2000m, result.TargetRiskDollars);
    }

    [Fact]
    public void PositionSize_CashConstrained()
    {
        // Equity $100K, risk 2%, price $200, ATR $5, multiplier 2
        // targetRisk = $2000, riskPerShare = $10, shares = 200
        // But only $10K cash → max shares = 10000/200 = 50
        var result = VolatilityTargeting.CalculatePositionSize(
            equity: 100_000m, riskPercent: 2m, price: 200m, atr: 5m,
            availableCash: 10_000m);

        Assert.Equal(50, result.Shares);
        Assert.Equal("VolTarget", result.Method);
    }

    [Fact]
    public void PositionSize_InsufficientCash_ReturnsZero()
    {
        // Cash so low can't buy even 1 share
        var result = VolatilityTargeting.CalculatePositionSize(
            equity: 100_000m, riskPercent: 1m, price: 500m, atr: 2m,
            availableCash: 100m);

        Assert.Equal(0, result.Shares);
        Assert.Equal("VolTarget_InsufficientCash", result.Method);
    }

    [Fact]
    public void PositionSize_ZeroEquity_ReturnsInvalid()
    {
        var result = VolatilityTargeting.CalculatePositionSize(
            equity: 0m, riskPercent: 1m, price: 50m, atr: 2m,
            availableCash: 10_000m);

        Assert.Equal(0, result.Shares);
        Assert.Equal("VolTarget_InvalidInputs", result.Method);
    }

    [Fact]
    public void PositionSize_ZeroAtr_ReturnsInvalid()
    {
        var result = VolatilityTargeting.CalculatePositionSize(
            equity: 100_000m, riskPercent: 1m, price: 50m, atr: 0m,
            availableCash: 100_000m);

        Assert.Equal(0, result.Shares);
        Assert.Equal("VolTarget_InvalidInputs", result.Method);
    }

    [Fact]
    public void PositionSize_VeryHighAtr_ZeroShares()
    {
        // ATR so high that even 1 share exceeds risk budget
        // Equity $100K, risk 0.5%, ATR $600, multiplier 2 → riskPerShare $1200, targetRisk $500
        var result = VolatilityTargeting.CalculatePositionSize(
            equity: 100_000m, riskPercent: 0.5m, price: 50m, atr: 600m,
            availableCash: 100_000m);

        Assert.Equal(0, result.Shares);
        Assert.Equal("VolTarget_ZeroShares", result.Method);
    }

    [Fact]
    public void PositionSize_WithSlippage_ReducesShares()
    {
        // Without slippage: equity $100K, risk 1%, price $100, ATR $5, mult 2
        // targetRisk = $1000, riskPerShare = $10, shares = 100
        // Cost = 100 × $100 = $10,000

        // With 0.5% slippage: cost = shares × $100 × 1.005 = shares × $100.50
        // Available cash = $10,000
        // Unconstrained shares = 100, cost = $10,050 > $10,000
        // Constrained: 10000 / 100.50 = 99 shares
        var result = VolatilityTargeting.CalculatePositionSize(
            equity: 100_000m, riskPercent: 1m, price: 100m, atr: 5m,
            availableCash: 10_000m, slippagePercent: 0.5m);

        Assert.Equal(99, result.Shares);
    }

    [Fact]
    public void PositionSize_WithCommission_ReducesCash()
    {
        // Available cash = $1000, commission = $10
        // Effective cash = $990, price = $50, ATR $2, mult 2
        // Unconstrained shares from risk: equity $100K, risk 1% = $1000 / $4 = 250
        // But cash constrained: 990 / 50 = 19 shares
        var result = VolatilityTargeting.CalculatePositionSize(
            equity: 100_000m, riskPercent: 1m, price: 50m, atr: 2m,
            availableCash: 1_000m, commissionPerTrade: 10m);

        Assert.Equal(19, result.Shares);
    }

    [Fact]
    public void PositionSize_CustomAtrMultiplier()
    {
        // Equity $100K, risk 1%, ATR $5, multiplier 1.5
        // targetRisk = $1000, riskPerShare = $7.50, shares = 133
        var result = VolatilityTargeting.CalculatePositionSize(
            equity: 100_000m, riskPercent: 1m, price: 50m, atr: 5m,
            availableCash: 100_000m, atrMultiplier: 1.5m);

        Assert.Equal(133, result.Shares);
        Assert.Equal(1.5m, result.AtrMultiplier);
        Assert.Equal(7.5m, result.RiskPerShare);
    }

    // ── Inverse relationship tests ──

    [Fact]
    public void InverseRelationship_DoubleAtr_HalfShares()
    {
        var sharesLowVol = VolatilityTargeting.CalculateShares(1000m, 5m, 2m);  // riskPS = $10
        var sharesHighVol = VolatilityTargeting.CalculateShares(1000m, 10m, 2m); // riskPS = $20

        Assert.Equal(100, sharesLowVol);
        Assert.Equal(50, sharesHighVol);
        Assert.Equal(sharesLowVol, sharesHighVol * 2); // exact 2:1 ratio
    }

    [Fact]
    public void InverseRelationship_TripleAtr_ThirdShares()
    {
        var sharesBase = VolatilityTargeting.CalculateShares(900m, 3m, 1m);  // 300 shares
        var sharesTriple = VolatilityTargeting.CalculateShares(900m, 9m, 1m); // 100 shares

        Assert.Equal(300, sharesBase);
        Assert.Equal(100, sharesTriple);
    }

    // ── Integration: Kelly + VolTarget ──

    [Fact]
    public void Integration_KellyDeterminesRisk_VolTargetDeterminesShares()
    {
        // Simulate: Kelly says risk 2%, equity $100K → $2000 risk budget
        // VolTarget with ATR $4, multiplier 2 → riskPerShare = $8 → 250 shares
        var kellyRiskPercent = 2m;
        var equity = 100_000m;
        var riskBudget = equity * kellyRiskPercent / 100m; // $2000

        var shares = VolatilityTargeting.CalculateShares(riskBudget, 4m, 2m);

        Assert.Equal(250, shares);

        // Same Kelly risk, but higher vol → fewer shares
        var sharesHighVol = VolatilityTargeting.CalculateShares(riskBudget, 10m, 2m);
        Assert.Equal(100, sharesHighVol);

        // Kelly risk budget unchanged, only share count differs
        Assert.True(shares > sharesHighVol);
    }

    // ── Known scenarios ──

    [Fact]
    public void KnownScenario_SP500_CalmMarket()
    {
        // S&P 500 stock at $150, ATR $3 (2% daily move), multiplier 2
        // Risk 1% of $100K = $1000
        // shares = $1000 / ($3 × 2) = 166
        var result = VolatilityTargeting.CalculatePositionSize(
            equity: 100_000m, riskPercent: 1m, price: 150m, atr: 3m,
            availableCash: 100_000m);

        Assert.Equal(166, result.Shares);
    }

    [Fact]
    public void KnownScenario_SmallCap_HighVol()
    {
        // Small cap at $25, ATR $4 (16% daily move), multiplier 2
        // Risk 1% of $50K = $500
        // shares = $500 / ($4 × 2) = 62
        var result = VolatilityTargeting.CalculatePositionSize(
            equity: 50_000m, riskPercent: 1m, price: 25m, atr: 4m,
            availableCash: 50_000m);

        Assert.Equal(62, result.Shares);
    }
}
