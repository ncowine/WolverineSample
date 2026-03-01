using Microsoft.EntityFrameworkCore;
using TradingAssistant.Application.Services;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Infrastructure.Persistence;
using TradingAssistant.SharedKernel;

namespace TradingAssistant.Application.Handlers.Trading;

public class GetTradeNotesHandler
{
    public static async Task<PagedResponse<TradeNoteDto>> HandleAsync(
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

        if (!string.IsNullOrWhiteSpace(query.Tag))
            notesQuery = notesQuery.Where(n => n.Tags.Contains(query.Tag));

        if (query.StartDate.HasValue)
            notesQuery = notesQuery.Where(n => n.CreatedAt >= query.StartDate.Value);

        if (query.EndDate.HasValue)
            notesQuery = notesQuery.Where(n => n.CreatedAt <= query.EndDate.Value);

        var totalCount = await notesQuery.CountAsync();

        var items = await notesQuery
            .OrderByDescending(n => n.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(n => new TradeNoteDto(
                n.Id, n.OrderId, n.PositionId,
                n.Content,
                string.IsNullOrEmpty(n.Tags)
                    ? new List<string>()
                    : n.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
                n.CreatedAt, n.UpdatedAt))
            .ToListAsync();

        return new PagedResponse<TradeNoteDto>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount
        };
    }
}
