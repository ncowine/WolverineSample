using TradingAssistant.Domain.Intelligence.Enums;
using TradingAssistant.SharedKernel;

namespace TradingAssistant.Domain.Intelligence;

public class TournamentEntry : BaseEntity
{
    public Guid TournamentRunId { get; set; }
    public Guid StrategyId { get; set; }
    public Guid PaperAccountId { get; set; }
    public string MarketCode { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public int DaysActive { get; set; }
    public int TotalTrades { get; set; }
    public decimal WinRate { get; set; }
    public decimal SharpeRatio { get; set; }
    public decimal MaxDrawdown { get; set; }
    public decimal TotalReturn { get; set; }
    public TournamentStatus Status { get; set; } = TournamentStatus.Active;
    public DateTime? PromotedAt { get; set; }
    public DateTime? RetiredAt { get; set; }

    /// <summary>
    /// Current allocation percentage (0-100).
    /// Gradual capital deployment: 25 → 50 → 100 as strategy proves itself.
    /// </summary>
    public decimal AllocationPercent { get; set; } = 25m;

    public string? RetirementReason { get; set; }
}
