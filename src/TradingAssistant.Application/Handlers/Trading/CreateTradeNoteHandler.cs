using Microsoft.EntityFrameworkCore;
using TradingAssistant.Application.Services;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Domain.Trading;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Trading;

public class CreateTradeNoteHandler
{
    public static async Task<TradeNoteDto> HandleAsync(
        CreateTradeNoteCommand command,
        TradingDbContext db,
        ICurrentUser currentUser)
    {
        if (command.OrderId.HasValue)
        {
            var order = await db.Orders
                .Include(o => o.Account)
                .FirstOrDefaultAsync(o => o.Id == command.OrderId.Value)
                ?? throw new InvalidOperationException($"Order '{command.OrderId}' not found.");

            if (order.Account.UserId != currentUser.UserId)
                throw new Application.Exceptions.ForbiddenAccessException("You do not have access to this order.");
        }

        if (command.PositionId.HasValue)
        {
            var position = await db.Positions
                .Include(p => p.Account)
                .FirstOrDefaultAsync(p => p.Id == command.PositionId.Value)
                ?? throw new InvalidOperationException($"Position '{command.PositionId}' not found.");

            if (position.Account.UserId != currentUser.UserId)
                throw new Application.Exceptions.ForbiddenAccessException("You do not have access to this position.");
        }

        var note = new TradeNote
        {
            UserId = currentUser.UserId,
            OrderId = command.OrderId,
            PositionId = command.PositionId,
            Content = command.Content.Trim()
        };

        db.TradeNotes.Add(note);
        await db.SaveChangesAsync();

        return new TradeNoteDto(
            note.Id, note.OrderId, note.PositionId,
            note.Content, note.CreatedAt, note.UpdatedAt);
    }
}
