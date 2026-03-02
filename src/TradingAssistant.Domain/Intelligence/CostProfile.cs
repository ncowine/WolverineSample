using TradingAssistant.SharedKernel;

namespace TradingAssistant.Domain.Intelligence;

public class CostProfile : BaseEntity
{
    public string MarketCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal CommissionPerShare { get; set; }
    public decimal CommissionPercent { get; set; }
    public decimal ExchangeFeePercent { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal SpreadEstimatePercent { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Estimate round-trip cost for a given trade.
    /// </summary>
    public decimal EstimateRoundTrip(decimal price, int shares)
    {
        var notional = price * shares;
        var perShareCost = CommissionPerShare * shares * 2; // buy + sell
        var percentCost = notional * (CommissionPercent + ExchangeFeePercent + TaxPercent + SpreadEstimatePercent) * 2 / 100;
        return perShareCost + percentCost;
    }
}
