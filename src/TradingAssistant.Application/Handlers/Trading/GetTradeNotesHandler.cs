using Microsoft.EntityFrameworkCore;
using TradingAssistant.Application.Services;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Trading;

public class GetTradeNotesHandler
{
    public static async Task<List<TradeNoteDto>> HandleAsync(
        GetTradeNotesQuery query,
        TradingDbContext db,
        ICurrentUser currentUser)
    {
        var notesQuery = db.TradeNotes
            .Where(n => n.UserId == currentUser.UserId);

        if (query.OrderId.HasValue)
            notesQuery = notesQuery.Where(n => n.OrderId == query.OrderId.Value);

        if (query.PositionId.HasValue)
            notesQuery = notesQuery.Where(n => n.PositionId == query.PositionId.Value);

        return await notesQuery
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new TradeNoteDto(
                n.Id, n.OrderId, n.PositionId,
                n.Content, n.CreatedAt, n.UpdatedAt))
            .ToListAsync();
    }
}
