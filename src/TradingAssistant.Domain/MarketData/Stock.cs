using TradingAssistant.SharedKernel;

namespace TradingAssistant.Domain.MarketData;

public class Stock : BaseEntity
{
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Exchange { get; set; } = string.Empty;
    public string Sector { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<PriceCandle> PriceCandles { get; set; } = new List<PriceCandle>();
    public ICollection<TechnicalIndicator> TechnicalIndicators { get; set; } = new List<TechnicalIndicator>();
}
