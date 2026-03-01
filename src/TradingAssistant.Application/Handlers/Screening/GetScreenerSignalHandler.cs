using Microsoft.EntityFrameworkCore;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Screening;

public class GetScreenerSignalHandler
{
    public static async Task<ScreenerSignalDto> HandleAsync(
        GetScreenerSignalQuery query,
        MarketDataDbContext db)
    {
        var latestRun = await db.ScreenerRuns
            .OrderByDescending(r => r.ScanDate)
            .FirstOrDefaultAsync();

        if (latestRun is null)
            throw new InvalidOperationException("No screener results available.");

        var signals = GetScreenerResultsHandler.DeserializeSignals(latestRun.ResultsJson);

        var signal = signals.FirstOrDefault(s =>
            s.Symbol.Equals(query.Symbol, StringComparison.OrdinalIgnoreCase));

        if (signal is null)
            throw new InvalidOperationException(
                $"No signal found for '{query.Symbol}' in latest scan ({latestRun.ScanDate:yyyy-MM-dd}).");

        return signal;
    }
}
