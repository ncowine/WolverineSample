using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradingAssistant.Contracts.Events;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Domain.Trading;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Trading;

public class FillOrderHandler
{
    public static async Task<OrderFilled> HandleAsync(
        OrderPlaced @event,
        TradingDbContext db,
        ILogger<FillOrderHandler> logger)
    {
        logger.LogInformation("[TradingDb] Filling order {OrderId} for {Symbol}", @event.OrderId, @event.Symbol);

        var order = await db.Orders
            .Include(o => o.Account)
            .FirstOrDefaultAsync(o => o.Id == @event.OrderId)
            ?? throw new InvalidOperationException($"Order '{@event.OrderId}' not found.");

        // Fill the order
        order.Status = OrderStatus.Filled;
        order.FilledAt = DateTime.UtcNow;

        // Calculate fee (0.1% of trade value)
        var fee = Math.Round(@event.Price * @event.Quantity * 0.001m, 2);

        // Create trade execution
        var execution = new TradeExecution
        {
            OrderId = order.Id,
            Symbol = @event.Symbol,
            Quantity = @event.Quantity,
            Price = @event.Price,
            Fee = fee,
            ExecutedAt = DateTime.UtcNow
        };
        db.TradeExecutions.Add(execution);

        // Create or update position
        var side = Enum.Parse<OrderSide>(@event.Side);
        var existingPosition = await db.Positions
            .FirstOrDefaultAsync(p => p.AccountId == @event.AccountId
                && p.Symbol == @event.Symbol
                && p.Status == PositionStatus.Open);

        if (side == OrderSide.Buy)
        {
            if (existingPosition != null)
            {
                // Average up/down
                var totalQty = existingPosition.Quantity + @event.Quantity;
                var totalCost = (existingPosition.AverageEntryPrice * existingPosition.Quantity)
                    + (@event.Price * @event.Quantity);
                existingPosition.AverageEntryPrice = Math.Round(totalCost / totalQty, 4);
                existingPosition.Quantity = totalQty;
                existingPosition.CurrentPrice = @event.Price;
            }
            else
            {
                db.Positions.Add(new Position
                {
                    AccountId = @event.AccountId,
                    Symbol = @event.Symbol,
                    Quantity = @event.Quantity,
                    AverageEntryPrice = @event.Price,
                    CurrentPrice = @event.Price,
                    Status = PositionStatus.Open,
                    OpenedAt = DateTime.UtcNow
                });
            }

            // Deduct fee
            order.Account.Balance -= fee;
        }
        else // Sell
        {
            if (existingPosition != null)
            {
                existingPosition.Quantity -= @event.Quantity;
                existingPosition.CurrentPrice = @event.Price;
                if (existingPosition.Quantity <= 0)
                {
                    existingPosition.Status = PositionStatus.Closed;
                    existingPosition.ClosedAt = DateTime.UtcNow;
                }

                // Credit sale proceeds
                order.Account.Balance += (@event.Price * @event.Quantity) - fee;
            }
        }

        // Update portfolio
        var portfolio = await db.Portfolios
            .FirstOrDefaultAsync(p => p.AccountId == @event.AccountId);

        if (portfolio != null)
        {
            var positions = await db.Positions
                .Where(p => p.AccountId == @event.AccountId && p.Status == PositionStatus.Open)
                .ToListAsync();

            portfolio.CashBalance = order.Account.Balance;
            portfolio.InvestedValue = positions.Sum(p => p.CurrentPrice * p.Quantity);
            portfolio.TotalValue = portfolio.CashBalance + portfolio.InvestedValue;
            portfolio.TotalPnL = positions.Sum(p => (p.CurrentPrice - p.AverageEntryPrice) * p.Quantity);
            portfolio.LastUpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        logger.LogInformation("[TradingDb] Order {OrderId} filled. Fee: {Fee}", @event.OrderId, fee);

        return new OrderFilled(
            order.Id, @event.AccountId, @event.Symbol,
            @event.Quantity, @event.Price, fee);
    }
}
