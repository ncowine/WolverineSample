using Microsoft.EntityFrameworkCore;
using TradingAssistant.Application.Exceptions;
using TradingAssistant.Application.Services;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.Events;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Domain.Trading;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Trading;

public class PlaceOrderHandler
{
    public static async Task<(OrderPlaced, OrderDto_Internal)> HandleAsync(
        PlaceOrderCommand command,
        TradingDbContext db,
        ICurrentUser currentUser)
    {
        var account = await db.Accounts.FindAsync(command.AccountId)
            ?? throw new InvalidOperationException($"Account '{command.AccountId}' not found.");

        if (account.UserId != currentUser.UserId)
            throw new ForbiddenAccessException("You do not have access to this account.");

        if (!Enum.TryParse<OrderSide>(command.Side, true, out var side))
            throw new InvalidOperationException($"Invalid order side: {command.Side}");

        if (!Enum.TryParse<OrderType>(command.Type, true, out var type))
            throw new InvalidOperationException($"Invalid order type: {command.Type}");

        // For market orders, simulate a price; for limit orders, use the provided price
        var executionPrice = command.Price ?? 100m; // Simplified: in reality, fetch from market data

        var totalCost = executionPrice * command.Quantity;

        if (side == OrderSide.Buy && account.Balance < totalCost)
            throw new InvalidOperationException($"Insufficient balance. Required: {totalCost:C}, Available: {account.Balance:C}");

        var order = new Order
        {
            AccountId = command.AccountId,
            Symbol = command.Symbol,
            Side = side,
            Type = type,
            Quantity = command.Quantity,
            Price = executionPrice,
            Status = OrderStatus.Pending
        };

        db.Orders.Add(order);

        // Reserve funds for buy orders
        if (side == OrderSide.Buy)
        {
            account.Balance -= totalCost;
        }

        await db.SaveChangesAsync();

        // Return cascading event (published via outbox)
        var @event = new OrderPlaced(
            order.Id, order.AccountId, order.Symbol,
            order.Side.ToString(), order.Quantity, executionPrice);

        return (@event, new OrderDto_Internal(order.Id));
    }
}

// Internal helper to carry the order ID - not part of contracts
public record OrderDto_Internal(Guid OrderId);
