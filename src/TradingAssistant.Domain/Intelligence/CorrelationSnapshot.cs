using TradingAssistant.SharedKernel;

namespace TradingAssistant.Domain.Intelligence;

public class CorrelationSnapshot : BaseEntity
{
    public DateTime SnapshotDate { get; set; }
    public int LookbackDays { get; set; } = 60;

    /// <summary>
    /// JSON: correlation matrix between all tracked markets.
    /// Example: {"US_SP500|IN_NIFTY50":0.45,"US_SP500|UK_FTSE100":0.78,...}
    /// </summary>
    public string MatrixJson { get; set; } = "{}";
}
