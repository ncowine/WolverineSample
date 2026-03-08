namespace TradingAssistant.Application.Intelligence;

/// <summary>
/// Pure data record for cost calculation (no DB dependency).
/// Mirrors the CostProfile entity fields for use in backtesting and paper trading.
/// </summary>
public record CostProfileData(
    string MarketCode,
    decimal CommissionPerShare,
    decimal CommissionPercent,
    decimal ExchangeFeePercent,
    decimal TaxPercent,
    decimal SpreadEstimatePercent,
    decimal StampDutyPercent = 0m,
    decimal FxFeePercent = 0m)
{
    /// <summary>
    /// US market: $0.005/share commission + 0.1% spread estimate.
    /// </summary>
    public static CostProfileData UsDefault => new(
        "US_SP500",
        CommissionPerShare: 0.005m,
        CommissionPercent: 0m,
        ExchangeFeePercent: 0m,
        TaxPercent: 0m,
        SpreadEstimatePercent: 0.1m);

    /// <summary>
    /// India market: 0.03% brokerage + 0.025% STT + 0.05% spread estimate.
    /// </summary>
    public static CostProfileData IndiaDefault => new(
        "IN_NIFTY50",
        CommissionPerShare: 0m,
        CommissionPercent: 0.03m,
        ExchangeFeePercent: 0m,
        TaxPercent: 0.025m,
        SpreadEstimatePercent: 0.05m);

    /// <summary>
    /// UK market (GBP stocks): 0.5% stamp duty on buys, 0.15% spread estimate.
    /// </summary>
    public static CostProfileData UkDefault => new(
        "UK_LSE",
        CommissionPerShare: 0m,
        CommissionPercent: 0m,
        ExchangeFeePercent: 0m,
        TaxPercent: 0m,
        SpreadEstimatePercent: 0.15m,
        StampDutyPercent: 0.5m);

    /// <summary>
    /// UK-based investor buying USD stocks: adds 0.5% FX conversion fee on top of US costs.
    /// </summary>
    public static CostProfileData UkUsdDefault => new(
        "UK_USD",
        CommissionPerShare: 0.005m,
        CommissionPercent: 0m,
        ExchangeFeePercent: 0m,
        TaxPercent: 0m,
        SpreadEstimatePercent: 0.1m,
        FxFeePercent: 0.5m);

    /// <summary>
    /// Get cost profile by market code string.
    /// </summary>
    public static CostProfileData ForMarket(string marketCode) => marketCode switch
    {
        "UK" or "UK_LSE" => UkDefault,
        "UK_USD" => UkUsdDefault,
        "IN" or "IN_NIFTY50" => IndiaDefault,
        _ => UsDefault,
    };
}

/// <summary>
/// Pure static cost calculator for market-specific transaction costs.
///
/// Computes costs per trade leg (buy or sell) based on:
/// - Per-share commission (e.g. US: $0.005/share)
/// - Percent-based fees (brokerage, exchange fees, tax, spread)
///
/// Used by BacktestEngine and paper trading to deduct realistic costs.
/// </summary>
public static class MarketCostCalculator
{
    /// <summary>
    /// Estimate cost for a single trade leg (buy or sell).
    /// </summary>
    /// <param name="price">Price per share.</param>
    /// <param name="shares">Number of shares.</param>
    /// <param name="profile">Cost profile for the market.</param>
    /// <param name="isBuy">True for buy side (stamp duty applies), false for sell.</param>
    /// <returns>Total cost for this trade leg.</returns>
    public static decimal EstimateTradeCost(decimal price, int shares, CostProfileData profile, bool isBuy = true)
    {
        if (shares <= 0 || price <= 0)
            return 0m;

        var notional = price * shares;
        var perShareCost = profile.CommissionPerShare * shares;
        var percentCost = notional *
            (profile.CommissionPercent + profile.ExchangeFeePercent +
             profile.TaxPercent + profile.SpreadEstimatePercent) / 100m;

        // Stamp duty applies on buys only (UK: 0.5% SDRT)
        var stampDuty = isBuy ? notional * profile.StampDutyPercent / 100m : 0m;

        // FX conversion fee applies on both sides
        var fxFee = notional * profile.FxFeePercent / 100m;

        return Math.Round(perShareCost + percentCost + stampDuty + fxFee, 2);
    }

    /// <summary>
    /// Estimate round-trip cost (buy + sell) for a trade.
    /// </summary>
    public static decimal EstimateRoundTripCost(decimal price, int shares, CostProfileData profile)
    {
        return EstimateTradeCost(price, shares, profile, isBuy: true) +
               EstimateTradeCost(price, shares, profile, isBuy: false);
    }

    /// <summary>
    /// Compute total percent-based cost rate (sum of all % fees).
    /// </summary>
    public static decimal TotalPercentRate(CostProfileData profile)
    {
        return profile.CommissionPercent + profile.ExchangeFeePercent +
               profile.TaxPercent + profile.SpreadEstimatePercent;
    }
}
