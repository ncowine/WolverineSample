using TradingAssistant.SharedKernel;

namespace TradingAssistant.Domain.MarketData;

public class BackfillJob : BaseEntity
{
    public Guid UniverseId { get; set; }
    public int YearsBack { get; set; } = 5;
    public bool IsIncremental { get; set; }

    public BackfillStatus Status { get; set; } = BackfillStatus.Pending;
    public int TotalSymbols { get; set; }
    public int CompletedSymbols { get; set; }
    public int FailedSymbols { get; set; }

    /// <summary>
    /// JSON array of error messages per symbol, e.g. [{"symbol":"XYZ","error":"..."}]
    /// </summary>
    public string ErrorLog { get; set; } = "[]";

    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public enum BackfillStatus
{
    Pending,
    Running,
    Completed,
    Failed
}
