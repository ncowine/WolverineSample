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

        if (command.Tags is not null)
        {
            var tags = command.Tags.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).ToList();
            note.Tags = string.Join(",", tags);
        }

        await db.SaveChangesAsync();

        var tagList = string.IsNullOrWhiteSpace(note.Tags)
            ? new List<string>()
            : note.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();

        return new TradeNoteDto(
            note.Id, note.OrderId, note.PositionId,
            note.Content, tagList, note.CreatedAt, note.UpdatedAt);
    }
}
