using Microsoft.EntityFrameworkCore;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.MarketData;

public class RemoveUniverseSymbolsHandler
{
    public static async Task<StockUniverseDto> HandleAsync(
        RemoveUniverseSymbolsCommand command,
        MarketDataDbContext db)
    {
        var universe = await db.StockUniverses.FindAsync(command.UniverseId)
            ?? throw new InvalidOperationException($"Universe {command.UniverseId} not found.");

        var toRemove = command.Symbols
            .Select(s => s.Trim().ToUpperInvariant())
            .ToHashSet();

        var remaining = universe.GetSymbolList()
            .Where(s => !toRemove.Contains(s))
            .ToList();

        universe.SetSymbolList(remaining);
        universe.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return CreateStockUniverseHandler.MapToDto(universe);
    }
}
