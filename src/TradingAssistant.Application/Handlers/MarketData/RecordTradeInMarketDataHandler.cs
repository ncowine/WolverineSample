using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradingAssistant.Contracts.Events;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.MarketData;

public class RecordTradeInMarketDataHandler
{
    public static async Task HandleAsync(
        OrderFilled @event,
        MarketDataDbContext db,
        ILogger<RecordTradeInMarketDataHandler> logger)
    {
        logger.LogInformation(
            "[MarketDataDb] Recording trade volume for {Symbol}: {Quantity} shares at {Price}",
            @event.Symbol, @event.Quantity, @event.Price);

        var stock = await db.Stocks.FirstOrDefaultAsync(s => s.Symbol == @event.Symbol);
        if (stock != null)
        {
            // Update last candle volume to reflect trade
            var latestCandle = await db.PriceCandles
                .Where(c => c.StockId == stock.Id)
                .OrderByDescending(c => c.Timestamp)
                .FirstOrDefaultAsync();

            if (latestCandle != null)
            {
                latestCandle.Volume += (long)@event.Quantity;
                latestCandle.Close = @event.Price;
                await db.SaveChangesAsync();
            }
        }

        logger.LogInformation("[MarketDataDb] Trade volume recorded for {Symbol}", @event.Symbol);
    }
}
