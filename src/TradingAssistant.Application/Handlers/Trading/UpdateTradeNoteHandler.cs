using Microsoft.EntityFrameworkCore;
using TradingAssistant.Application.Exceptions;
using TradingAssistant.Application.Services;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Trading;

public class UpdateTradeNoteHandler
{
    public static async Task<TradeNoteDto> HandleAsync(
        UpdateTradeNoteCommand command,
        TradingDbContext db,
        ICurrentUser currentUser)
    {
        var note = await db.TradeNotes
            .FirstOrDefaultAsync(n => n.Id == command.NoteId)
            ?? throw new InvalidOperationException($"Trade note '{command.NoteId}' not found.");

        if (note.UserId != currentUser.UserId)
            throw new ForbiddenAccessException("You do not have access to this note.");

        note.Content = command.Content.Trim();
        note.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return new TradeNoteDto(
            note.Id, note.OrderId, note.PositionId,
            note.Content, note.CreatedAt, note.UpdatedAt);
    }
}
