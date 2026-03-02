using TradingAssistant.SharedKernel;

namespace TradingAssistant.Domain.Intelligence;

public class MarketProfile : BaseEntity
{
    public string MarketCode { get; set; } = string.Empty;
    public string Exchange { get; set; } = string.Empty;
    public string Currency { get; set; } = "USD";
    public string Timezone { get; set; } = "America/New_York";
    public string VixSymbol { get; set; } = string.Empty;
    public string DataSource { get; set; } = "yahoo";
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// JSON: trading hours, regime thresholds, cost model, and behavioral characteristics.
    /// Example: {"tradingHours":{"open":"09:30","close":"16:00"},"regimeThresholds":{"highVol":30,"bullBreadth":0.60,"bearBreadth":0.40},...}
    /// </summary>
    public string ConfigJson { get; set; } = "{}";

    /// <summary>
    /// JSON: Claude-generated market DNA profile (trending vs mean-reverting, avg volatility, sector weights, cross-market correlations).
    /// Updated quarterly.
    /// </summary>
    public string DnaProfileJson { get; set; } = "{}";

    public DateTime? DnaProfileUpdatedAt { get; set; }
}
