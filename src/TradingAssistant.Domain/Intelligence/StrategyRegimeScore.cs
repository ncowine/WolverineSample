using TradingAssistant.Domain.Intelligence.Enums;
using TradingAssistant.SharedKernel;

namespace TradingAssistant.Domain.Intelligence;

/// <summary>
/// Regime-performance matrix entry: Sharpe ratio for a strategy in a specific regime.
/// Populated from walk-forward results or backtest results during strategy generation.
/// </summary>
public class StrategyRegimeScore : BaseEntity
{
    public Guid StrategyId { get; set; }
    public string MarketCode { get; set; } = string.Empty;
    public RegimeType Regime { get; set; }
    public decimal SharpeRatio { get; set; }

    /// <summary>
    /// Number of backtest/walk-forward samples contributing to this score.
    /// </summary>
    public int SampleSize { get; set; } = 1;

    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
}
