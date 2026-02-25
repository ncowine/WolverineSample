using TradingAssistant.Domain.Enums;
using TradingAssistant.SharedKernel;

namespace TradingAssistant.Domain.MarketData;

public class PriceCandle : BaseEntity
{
    public Guid StockId { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }
    public DateTime Timestamp { get; set; }
    public CandleInterval Interval { get; set; }

    public Stock Stock { get; set; } = null!;
}
