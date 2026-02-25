using Microsoft.EntityFrameworkCore;
using TradingAssistant.Application.Exceptions;
using TradingAssistant.Application.Services;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.Events;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Trading;

public class ClosePositionHandler
{
    public static async Task<PositionClosed> HandleAsync(
        ClosePositionCommand command,
        TradingDbContext db,
        ICurrentUser currentUser)
    {
        var position = await db.Positions
            .Include(p => p.Account)
            .FirstOrDefaultAsync(p => p.Id == command.PositionId)
            ?? throw new InvalidOperationException($"Position '{command.PositionId}' not found.");

        if (position.Account.UserId != currentUser.UserId)
            throw new ForbiddenAccessException("You do not have access to this position.");

        if (position.Status != PositionStatus.Open)
            throw new InvalidOperationException("Position is already closed.");

        var pnl = (position.CurrentPrice - position.AverageEntryPrice) * position.Quantity;

        position.Status = PositionStatus.Closed;
        position.ClosedAt = DateTime.UtcNow;

        // Credit proceeds to account
        position.Account.Balance += (position.CurrentPrice * position.Quantity);

        // Update portfolio
        var portfolio = await db.Portfolios
            .FirstOrDefaultAsync(p => p.AccountId == position.AccountId);

        if (portfolio != null)
        {
            portfolio.CashBalance = position.Account.Balance;
            portfolio.InvestedValue -= (position.CurrentPrice * position.Quantity);
            portfolio.TotalValue = portfolio.CashBalance + portfolio.InvestedValue;
            portfolio.TotalPnL += pnl;
            portfolio.LastUpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        return new PositionClosed(
            position.Id, position.AccountId, position.Symbol,
            position.Quantity, position.CurrentPrice, Math.Round(pnl, 2));
    }
}
