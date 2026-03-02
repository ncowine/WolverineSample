using TradingAssistant.SharedKernel;

namespace TradingAssistant.Domain.Intelligence;

public class BreadthSnapshot : BaseEntity
{
    public string MarketCode { get; set; } = string.Empty;
    public DateTime SnapshotDate { get; set; }
    public decimal AdvanceDeclineRatio { get; set; }
    public decimal PctAbove200Sma { get; set; }
    public decimal PctAbove50Sma { get; set; }
    public int NewHighs { get; set; }
    public int NewLows { get; set; }
    public int TotalStocks { get; set; }
    public int Advancing { get; set; }
    public int Declining { get; set; }
}
