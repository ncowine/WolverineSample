using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Domain.Trading;
using TradingAssistant.Infrastructure.Caching;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Trading;

public static class ExecuteDcaPlanHandler
{
    public static async Task<DcaExecution> ExecuteAsync(
        DcaPlan plan,
        TradingDbContext db,
        StockPriceCache priceCache,
        ILogger logger)
    {
        var account = await db.Accounts
            .Include(a => a.Portfolio)
            .Include(a => a.Positions)
            .FirstOrDefaultAsync(a => a.Id == plan.AccountId);

        if (account is null)
        {
            plan.IsActive = false;
            return await RecordExecution(db, plan, DcaExecutionStatus.Error, errorReason: "Account not found");
        }

        // Get current price from cache
        var priceData = await priceCache.Get(plan.Symbol);
        if (priceData is null || priceData.CurrentPrice <= 0)
        {
            plan.IsActive = false;
            return await RecordExecution(db, plan, DcaExecutionStatus.StockNotFound,
                errorReason: $"No market price available for '{plan.Symbol}'");
        }

        var price = priceData.CurrentPrice;
        var quantity = Math.Floor(plan.Amount / price);

        if (quantity <= 0)
        {
            return await RecordExecution(db, plan, DcaExecutionStatus.InsufficientFunds,
                errorReason: $"Amount {plan.Amount:C} is too small to buy one share at {price:C}");
        }

        var totalCost = quantity * price;

        if (account.Balance < totalCost)
        {
            plan.IsActive = false;
            return await RecordExecution(db, plan, DcaExecutionStatus.InsufficientFunds,
                errorReason: $"Insufficient balance. Required: {totalCost:C}, Available: {account.Balance:C}");
        }

        // Create order
        var order = new Order
        {
            AccountId = account.Id,
            Symbol = plan.Symbol,
            Side = OrderSide.Buy,
            Type = OrderType.Market,
            Quantity = quantity,
            Price = price,
            Status = OrderStatus.Pending
        };
        db.Orders.Add(order);

        // Reserve funds and fill immediately
        account.Balance -= totalCost;
        order.Status = OrderStatus.Filled;
        order.FilledAt = DateTime.UtcNow;

        // Paper accounts trade fee-free; live accounts pay 0.1%
        var fee = account.AccountType == AccountType.Paper
            ? 0m
            : Math.Round(price * quantity * 0.001m, 2);

        account.Balance -= fee;

        // Create trade execution
        var tradeExecution = new TradeExecution
        {
            OrderId = order.Id,
            Symbol = plan.Symbol,
            Quantity = quantity,
            Price = price,
            Fee = fee,
            ExecutedAt = DateTime.UtcNow
        };
        db.TradeExecutions.Add(tradeExecution);

        // Update or create position
        var existingPosition = account.Positions
            .FirstOrDefault(p => p.Symbol == plan.Symbol && p.Status == PositionStatus.Open);

        if (existingPosition != null)
        {
            var totalQty = existingPosition.Quantity + quantity;
            var totalPositionCost = (existingPosition.AverageEntryPrice * existingPosition.Quantity)
                + (price * quantity);
            existingPosition.AverageEntryPrice = Math.Round(totalPositionCost / totalQty, 4);
            existingPosition.Quantity = totalQty;
            existingPosition.CurrentPrice = price;
        }
        else
        {
            db.Positions.Add(new Position
            {
                AccountId = account.Id,
                Symbol = plan.Symbol,
                Quantity = quantity,
                AverageEntryPrice = price,
                CurrentPrice = price,
                Status = PositionStatus.Open,
                OpenedAt = DateTime.UtcNow
            });
        }

        // Update portfolio
        if (account.Portfolio != null)
        {
            var openPositions = await db.Positions
                .Where(p => p.AccountId == account.Id && p.Status == PositionStatus.Open)
                .ToListAsync();

            account.Portfolio.CashBalance = account.Balance;
            account.Portfolio.InvestedValue = openPositions.Sum(p => p.CurrentPrice * p.Quantity);
            account.Portfolio.TotalValue = account.Portfolio.CashBalance + account.Portfolio.InvestedValue;
            account.Portfolio.TotalPnL = openPositions.Sum(p => (p.CurrentPrice - p.AverageEntryPrice) * p.Quantity);
            account.Portfolio.LastUpdatedAt = DateTime.UtcNow;
        }

        // Record DCA execution
        var dcaExecution = new DcaExecution
        {
            DcaPlanId = plan.Id,
            OrderId = order.Id,
            Amount = plan.Amount,
            ExecutedPrice = price,
            Quantity = quantity,
            Status = DcaExecutionStatus.Success
        };
        db.DcaExecutions.Add(dcaExecution);

        // Advance next execution date
        plan.NextExecutionDate = CreateDcaPlanHandler.CalculateNextExecution(plan.Frequency);

        await db.SaveChangesAsync();

        logger.LogInformation(
            "DCA plan {PlanId} executed: {Quantity} shares of {Symbol} at {Price} (fee: {Fee})",
            plan.Id, quantity, plan.Symbol, price, fee);

        return dcaExecution;
    }

    private static async Task<DcaExecution> RecordExecution(
        TradingDbContext db,
        DcaPlan plan,
        DcaExecutionStatus status,
        string? errorReason = null)
    {
        var execution = new DcaExecution
        {
            DcaPlanId = plan.Id,
            Amount = plan.Amount,
            Status = status,
            ErrorReason = errorReason
        };
        db.DcaExecutions.Add(execution);

        // Still advance the schedule on failure (except deactivation) to avoid retry storms
        if (plan.IsActive)
            plan.NextExecutionDate = CreateDcaPlanHandler.CalculateNextExecution(plan.Frequency);

        await db.SaveChangesAsync();
        return execution;
    }
}
