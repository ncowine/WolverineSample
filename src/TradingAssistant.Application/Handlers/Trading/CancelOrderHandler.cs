using Microsoft.EntityFrameworkCore;
using TradingAssistant.Application.Exceptions;
using TradingAssistant.Application.Services;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.Events;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Trading;

public class CancelOrderHandler
{
    public static async Task<OrderCancelled> HandleAsync(
        CancelOrderCommand command,
        TradingDbContext db,
        ICurrentUser currentUser)
    {
        var order = await db.Orders
            .Include(o => o.Account)
            .FirstOrDefaultAsync(o => o.Id == command.OrderId)
            ?? throw new InvalidOperationException($"Order '{command.OrderId}' not found.");

        if (order.Account.UserId != currentUser.UserId)
            throw new ForbiddenAccessException("You do not have access to this order.");

        if (order.Status != OrderStatus.Pending)
            throw new InvalidOperationException($"Cannot cancel order with status '{order.Status}'.");

        order.Status = OrderStatus.Cancelled;

        // Refund reserved funds for buy orders
        if (order.Side == OrderSide.Buy && order.Price.HasValue)
        {
            order.Account.Balance += order.Price.Value * order.Quantity;
        }

        await db.SaveChangesAsync();

        return new OrderCancelled(order.Id, order.AccountId);
    }
}
