using Microsoft.EntityFrameworkCore;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.MarketData;

public class GetStockUniversesHandler
{
    public static async Task<List<StockUniverseDto>> HandleAsync(
        GetStockUniversesQuery query,
        MarketDataDbContext db)
    {
        var universes = await db.StockUniverses
            .OrderBy(u => u.Name)
            .ToListAsync();

        return universes.Select(CreateStockUniverseHandler.MapToDto).ToList();
    }
}
