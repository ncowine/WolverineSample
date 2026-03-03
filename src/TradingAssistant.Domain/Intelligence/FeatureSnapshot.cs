using TradingAssistant.Domain.Intelligence.Enums;
using TradingAssistant.SharedKernel;

namespace TradingAssistant.Domain.Intelligence;

/// <summary>
/// Captures the full indicator state at trade entry for ML model training.
/// Features stored as compressed JSON with SHA256 integrity hash.
/// </summary>
public class FeatureSnapshot : BaseEntity
{
    /// <summary>Guid reference to TradingDb.Order (cross-DB, no FK).</summary>
    public Guid TradeId { get; set; }

    public string Symbol { get; set; } = string.Empty;
    public string MarketCode { get; set; } = string.Empty;
    public DateTime CapturedAt { get; set; }

    /// <summary>Schema version for feature compatibility tracking.</summary>
    public int FeatureVersion { get; set; } = 1;

    /// <summary>Number of features captured in this snapshot.</summary>
    public int FeatureCount { get; set; }

    /// <summary>GZip-compressed, Base64-encoded JSON of feature dictionary.</summary>
    public string FeaturesJson { get; set; } = string.Empty;

    /// <summary>SHA256 hex hash of the uncompressed JSON for integrity verification.</summary>
    public string FeaturesHash { get; set; } = string.Empty;

    /// <summary>Trade outcome (updated when position closes).</summary>
    public TradeOutcome TradeOutcome { get; set; } = TradeOutcome.Pending;

    /// <summary>Realized P&L percentage (updated on trade close).</summary>
    public decimal? TradePnlPercent { get; set; }

    public DateTime? OutcomeUpdatedAt { get; set; }
}
