using Microsoft.EntityFrameworkCore;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Domain.MarketData;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.MarketData;

public class CreateStockUniverseHandler
{
    public static async Task<StockUniverseDto> HandleAsync(
        CreateStockUniverseCommand command,
        MarketDataDbContext db)
    {
        var name = command.Name.Trim();

        var exists = await db.StockUniverses.AnyAsync(u => u.Name == name);
        if (exists)
            throw new InvalidOperationException($"A universe named '{name}' already exists.");

        var universe = new StockUniverse
        {
            Name = name,
            Description = command.Description?.Trim() ?? string.Empty
        };

        if (command.Symbols is { Count: > 0 })
        {
            universe.SetSymbolList(command.Symbols);
        }

        db.StockUniverses.Add(universe);
        await db.SaveChangesAsync();

        return MapToDto(universe);
    }

    internal static StockUniverseDto MapToDto(StockUniverse u) =>
        new(u.Id, u.Name, u.Description, u.GetSymbolList(), u.IsActive, u.IncludesBenchmark, u.CreatedAt);
}
