using TradingAssistant.Domain.Intelligence.Enums;
using TradingAssistant.SharedKernel;

namespace TradingAssistant.Domain.Intelligence;

/// <summary>
/// Tracks which strategy is currently assigned to a market.
/// Updated when regime changes trigger best-strategy selection.
/// </summary>
public class StrategyAssignment : BaseEntity
{
    public string MarketCode { get; set; } = string.Empty;
    public Guid StrategyId { get; set; }
    public string StrategyName { get; set; } = string.Empty;
    public RegimeType Regime { get; set; }

    /// <summary>
    /// Current allocation percentage (50 → 100 during gradual switchover).
    /// </summary>
    public decimal AllocationPercent { get; set; } = 50m;

    /// <summary>
    /// When true, this assignment is locked by the user and won't be changed on regime transitions.
    /// </summary>
    public bool IsLocked { get; set; }

    /// <summary>
    /// When the switchover started (allocation ramps from 50% to 100% over SwitchoverDays).
    /// </summary>
    public DateTime SwitchoverStartDate { get; set; }

    /// <summary>
    /// When the assignment was created or last updated.
    /// </summary>
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}
