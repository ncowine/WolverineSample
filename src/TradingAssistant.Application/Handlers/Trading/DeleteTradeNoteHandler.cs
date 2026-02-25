using Microsoft.EntityFrameworkCore;
using TradingAssistant.Application.Exceptions;
using TradingAssistant.Application.Services;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Trading;

public class DeleteTradeNoteHandler
{
    public static async Task<string> HandleAsync(
        DeleteTradeNoteCommand command,
        TradingDbContext db,
        ICurrentUser currentUser)
    {
        var note = await db.TradeNotes
            .FirstOrDefaultAsync(n => n.Id == command.NoteId)
            ?? throw new InvalidOperationException($"Trade note '{command.NoteId}' not found.");

        if (note.UserId != currentUser.UserId)
            throw new ForbiddenAccessException("You do not have access to this note.");

        db.TradeNotes.Remove(note);
        await db.SaveChangesAsync();

        return "Trade note deleted successfully.";
    }
}
