using TradingAssistant.Domain.Intelligence.Enums;
using TradingAssistant.SharedKernel;

namespace TradingAssistant.Domain.Intelligence;

public class RegimeTransition : BaseEntity
{
    public string MarketCode { get; set; } = string.Empty;
    public RegimeType FromRegime { get; set; }
    public RegimeType ToRegime { get; set; }
    public DateTime TransitionDate { get; set; }
    public decimal SmaSlope50 { get; set; }
    public decimal SmaSlope200 { get; set; }
    public decimal VixLevel { get; set; }
    public decimal BreadthScore { get; set; }
    public decimal PctAbove200Sma { get; set; }
}
