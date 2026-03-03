using TradingAssistant.Domain.Intelligence.Enums;
using TradingAssistant.SharedKernel;

namespace TradingAssistant.Domain.Intelligence;

public class TournamentRun : BaseEntity
{
    public string MarketCode { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public TournamentRunStatus Status { get; set; } = TournamentRunStatus.Active;
    public int MaxEntries { get; set; } = 20;
    public string Description { get; set; } = string.Empty;
    public int EntryCount { get; set; }
}
