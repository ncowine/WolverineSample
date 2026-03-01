using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
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
            .WithSummary("Create a trade note on an order or position");

        group.MapGet("/", GetNotes)
            .WithSummary("Get trade notes, optionally filtered by order or position");

        // Route param + body — needs manual handler
        group.MapPut("/{noteId}", UpdateNote)
            .WithSummary("Update an existing trade note");

        group.MapDelete("/{noteId}", DeleteNote)
            .WithSummary("Delete a trade note");
    }

    private static async Task<List<TradeNoteDto>> GetNotes(
        Guid? orderId, Guid? positionId, IMessageBus bus)
    {
        return await bus.InvokeAsync<List<TradeNoteDto>>(
            new GetTradeNotesQuery(orderId, positionId));
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
