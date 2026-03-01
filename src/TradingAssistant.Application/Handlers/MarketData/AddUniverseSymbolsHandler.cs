using Microsoft.EntityFrameworkCore;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.MarketData;

public class AddUniverseSymbolsHandler
{
    public static async Task<StockUniverseDto> HandleAsync(
        AddUniverseSymbolsCommand command,
        MarketDataDbContext db)
    {
        var universe = await db.StockUniverses.FindAsync(command.UniverseId)
            ?? throw new InvalidOperationException($"Universe {command.UniverseId} not found.");

        var existing = universe.GetSymbolList();
        var merged = existing
            .Concat(command.Symbols.Select(s => s.Trim().ToUpperInvariant()))
            .Distinct()
            .OrderBy(s => s)
            .ToList();

        universe.SetSymbolList(merged);
        universe.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return CreateStockUniverseHandler.MapToDto(universe);
    }
}
