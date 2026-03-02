using TradingAssistant.SharedKernel;

namespace TradingAssistant.Domain.Intelligence;

/// <summary>
/// Persistent record of a circuit breaker activation or deactivation event.
/// Stored in IntelligenceDbContext for audit and analysis.
/// </summary>
public class CircuitBreakerEvent : BaseEntity
{
    public string AccountId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty; // "Activated" or "Deactivated"
    public DateTime EventDate { get; set; }
    public decimal PeakEquity { get; set; }
    public decimal CurrentEquity { get; set; }
    public decimal DrawdownPercent { get; set; }
    public decimal ThresholdPercent { get; set; }
    public string? RegimeAtEvent { get; set; }
    public decimal? RegimeConfidence { get; set; }
    public int PendingOrdersCancelled { get; set; }
    public int OpenPositionsAtEvent { get; set; }
    public string? DeactivationReason { get; set; }
}
