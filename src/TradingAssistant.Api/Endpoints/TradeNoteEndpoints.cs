using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.SharedKernel;
using Wolverine;
using Wolverine.Http;

namespace TradingAssistant.Api.Endpoints;

public class TradeNoteEndpoints : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/trading/notes")
            .WithTags("Trade Notes")
            .RequireAuthorization();

        group.MapPostToWolverine<CreateTradeNoteCommand, TradeNoteDto>("/")
            .WithSummary("Create a trade note with optional tags");

        group.MapGet("/", GetNotes)
            .WithSummary("Get trade notes filtered by order, position, tag, or date range");

        // Route param + body — needs manual handler
        group.MapPut("/{noteId}", UpdateNote)
            .WithSummary("Update an existing trade note and its tags");

        group.MapDelete("/{noteId}", DeleteNote)
            .WithSummary("Delete a trade note");
    }

    private static async Task<PagedResponse<TradeNoteDto>> GetNotes(
        Guid? orderId, Guid? positionId, string? tag,
        DateTime? startDate, DateTime? endDate,
        int? page, int? pageSize,
        IMessageBus bus)
    {
        return await bus.InvokeAsync<PagedResponse<TradeNoteDto>>(
            new GetTradeNotesQuery(orderId, positionId, tag, startDate, endDate,
                page ?? 1, pageSize ?? 20));
    }

    private static async Task<TradeNoteDto> UpdateNote(
        Guid noteId, UpdateTradeNoteCommand command, IMessageBus bus)
    {
        return await bus.InvokeAsync<TradeNoteDto>(
            command with { NoteId = noteId });
    }

    private static async Task<string> DeleteNote(
        Guid noteId, IMessageBus bus)
    {
        return await bus.InvokeAsync<string>(
            new DeleteTradeNoteCommand(noteId));
    }
}
