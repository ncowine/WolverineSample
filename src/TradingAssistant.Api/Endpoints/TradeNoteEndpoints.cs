using Microsoft.AspNetCore.Authorization;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using Wolverine;
using Wolverine.Http;

namespace TradingAssistant.Api.Endpoints;

public static class TradeNoteEndpoints
{
    [Authorize]
    [WolverinePost("/api/trading/notes")]
    public static async Task<TradeNoteDto> CreateNote(
        CreateTradeNoteCommand command, IMessageBus bus)
    {
        return await bus.InvokeAsync<TradeNoteDto>(command);
    }

    [Authorize]
    [WolverineGet("/api/trading/notes")]
    public static async Task<List<TradeNoteDto>> GetNotes(
        Guid? orderId, Guid? positionId, IMessageBus bus)
    {
        return await bus.InvokeAsync<List<TradeNoteDto>>(
            new GetTradeNotesQuery(orderId, positionId));
    }

    [Authorize]
    [WolverinePut("/api/trading/notes/{noteId}")]
    public static async Task<TradeNoteDto> UpdateNote(
        Guid noteId, UpdateTradeNoteCommand command, IMessageBus bus)
    {
        return await bus.InvokeAsync<TradeNoteDto>(
            command with { NoteId = noteId });
    }

    [Authorize]
    [WolverineDelete("/api/trading/notes/{noteId}")]
    public static async Task<string> DeleteNote(
        Guid noteId, IMessageBus bus)
    {
        return await bus.InvokeAsync<string>(
            new DeleteTradeNoteCommand(noteId));
    }
}
