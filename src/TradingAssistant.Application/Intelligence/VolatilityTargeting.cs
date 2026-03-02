namespace TradingAssistant.Application.Intelligence;

/// <summary>
/// Result of volatility-targeted position sizing.
/// </summary>
public record VolTargetResult(
    int Shares,
    decimal TargetRiskDollars,
    decimal AtrUsed,
    decimal AtrMultiplier,
    decimal RiskPerShare,
    string Method);

/// <summary>
/// Pure static volatility-targeted position sizing calculator.
///
/// Core formula: shares = targetRisk($) / (ATR × multiplier)
///
/// Scales position size inversely with volatility:
/// - Low ATR (calm market) → larger positions
/// - High ATR (volatile market) → smaller positions
///
/// Designed to layer on top of Kelly or fixed risk sizing:
/// Kelly/Fixed determines the risk budget, vol-targeting determines share count.
/// </summary>
public static class VolatilityTargeting
{
    public const decimal DefaultAtrMultiplier = 2m;

    /// <summary>
    /// Calculate the number of shares using volatility targeting.
    /// shares = targetRiskDollars / (atr × atrMultiplier)
    /// </summary>
    /// <param name="targetRiskDollars">Dollar amount to risk (from Kelly or fixed %)</param>
    /// <param name="atr">Current ATR value for the instrument</param>
    /// <param name="atrMultiplier">Multiplier applied to ATR (default 2.0)</param>
    /// <returns>Number of shares (floored to integer), or 0 if inputs are invalid</returns>
    public static int CalculateShares(decimal targetRiskDollars, decimal atr, decimal atrMultiplier = DefaultAtrMultiplier)
    {
        if (targetRiskDollars <= 0 || atr <= 0 || atrMultiplier <= 0)
            return 0;

        var riskPerShare = atr * atrMultiplier;
        var shares = (int)(targetRiskDollars / riskPerShare);

        return Math.Max(0, shares);
    }

    /// <summary>
    /// Full volatility-targeted sizing: compute shares from equity, risk percent, and ATR.
    ///
    /// Flow:
    /// 1. Calculate target risk in dollars: equity × riskPercent / 100
    /// 2. Calculate risk per share: ATR × atrMultiplier
    /// 3. shares = targetRisk / riskPerShare
    /// 4. Clamp shares so total cost doesn't exceed available cash
    /// </summary>
    public static VolTargetResult CalculatePositionSize(
        decimal equity,
        decimal riskPercent,
        decimal price,
        decimal atr,
        decimal availableCash,
        decimal atrMultiplier = DefaultAtrMultiplier,
        decimal commissionPerTrade = 0m,
        decimal slippagePercent = 0m)
    {
        if (equity <= 0 || riskPercent <= 0 || price <= 0 || atr <= 0 || atrMultiplier <= 0)
        {
            return new VolTargetResult(
                Shares: 0,
                TargetRiskDollars: 0m,
                AtrUsed: atr,
                AtrMultiplier: atrMultiplier,
                RiskPerShare: 0m,
                Method: "VolTarget_InvalidInputs");
        }

        var targetRiskDollars = equity * riskPercent / 100m;
        var riskPerShare = atr * atrMultiplier;
        var shares = (int)(targetRiskDollars / riskPerShare);

        if (shares <= 0)
        {
            return new VolTargetResult(
                Shares: 0,
                TargetRiskDollars: targetRiskDollars,
                AtrUsed: atr,
                AtrMultiplier: atrMultiplier,
                RiskPerShare: riskPerShare,
                Method: "VolTarget_ZeroShares");
        }

        // Clamp to available cash
        var estimatedCost = shares * price * (1 + slippagePercent / 100m) + commissionPerTrade;
        if (estimatedCost > availableCash)
        {
            shares = (int)((availableCash - commissionPerTrade) / (price * (1 + slippagePercent / 100m)));
            if (shares <= 0)
            {
                return new VolTargetResult(
                    Shares: 0,
                    TargetRiskDollars: targetRiskDollars,
                    AtrUsed: atr,
                    AtrMultiplier: atrMultiplier,
                    RiskPerShare: riskPerShare,
                    Method: "VolTarget_InsufficientCash");
            }
        }

        return new VolTargetResult(
            Shares: shares,
            TargetRiskDollars: targetRiskDollars,
            AtrUsed: atr,
            AtrMultiplier: atrMultiplier,
            RiskPerShare: riskPerShare,
            Method: "VolTarget");
    }
}
