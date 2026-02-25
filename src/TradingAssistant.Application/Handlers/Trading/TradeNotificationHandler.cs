using Microsoft.Extensions.Logging;
using TradingAssistant.Contracts.Events;

namespace TradingAssistant.Application.Handlers.Trading;

public class TradeNotificationHandler
{
    public static void Handle(
        OrderFilled @event,
        ILogger<TradeNotificationHandler> logger)
    {
        logger.LogInformation(
            "🔔 [NOTIFICATION] Order {OrderId} filled: {Symbol} {Quantity} shares at ${Price} (Fee: ${Fee})",
            @event.OrderId, @event.Symbol, @event.Quantity, @event.Price, @event.Fee);
    }
}
