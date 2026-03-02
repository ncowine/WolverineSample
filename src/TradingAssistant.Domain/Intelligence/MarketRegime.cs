using TradingAssistant.Domain.Intelligence.Enums;
using TradingAssistant.SharedKernel;

namespace TradingAssistant.Domain.Intelligence;

public class MarketRegime : BaseEntity
{
    public string MarketCode { get; set; } = string.Empty;
    public RegimeType CurrentRegime { get; set; }
    public DateTime RegimeStartDate { get; set; }
    public int RegimeDuration { get; set; }
    public decimal SmaSlope50 { get; set; }
    public decimal SmaSlope200 { get; set; }
    public decimal VixLevel { get; set; }
    public decimal BreadthScore { get; set; }
    public decimal PctAbove200Sma { get; set; }
    public decimal AdvanceDeclineRatio { get; set; }
    public DateTime ClassifiedAt { get; set; } = DateTime.UtcNow;
    public decimal ConfidenceScore { get; set; }
}
