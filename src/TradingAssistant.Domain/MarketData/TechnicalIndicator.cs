using TradingAssistant.Domain.Enums;
using TradingAssistant.SharedKernel;

namespace TradingAssistant.Domain.MarketData;

public class TechnicalIndicator : BaseEntity
{
    public Guid StockId { get; set; }
    public IndicatorType IndicatorType { get; set; }
    public decimal Value { get; set; }
    public DateTime Timestamp { get; set; }

    public Stock Stock { get; set; } = null!;
}
