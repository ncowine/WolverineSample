using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TradingAssistant.Application.Screening;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Screening;

public class GetScreenerResultsHandler
{
    public static async Task<ScreenerResultsDto> HandleAsync(
        GetScreenerResultsQuery query,
        MarketDataDbContext db)
    {
        var runsQuery = db.ScreenerRuns.AsQueryable();

        if (query.Date.HasValue)
            runsQuery = runsQuery.Where(r => r.ScanDate.Date == query.Date.Value.Date);

        var run = await runsQuery
            .OrderByDescending(r => r.ScanDate)
            .FirstOrDefaultAsync();

        if (run is null)
        {
            return new ScreenerResultsDto(
                Guid.Empty, DateTime.MinValue, "", 0, 0, 0,
                new List<ScreenerSignalDto>(), new List<string> { "No screener results found" });
        }

        var signals = DeserializeSignals(run.ResultsJson);
        var warnings = DeserializeWarnings(run.WarningsJson);

        // Filter by grade if requested
        if (!string.IsNullOrEmpty(query.MinGrade) && Enum.TryParse<SignalGrade>(query.MinGrade, out var minGrade))
        {
            signals = signals.Where(s =>
                Enum.TryParse<SignalGrade>(s.Grade, out var g) && g <= minGrade).ToList();
        }

        return new ScreenerResultsDto(
            run.Id, run.ScanDate, run.StrategyName,
            run.SymbolsScanned, run.SignalsFound,
            signals.Count, signals, warnings);
    }

    internal static List<ScreenerSignalDto> DeserializeSignals(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<ScreenerSignalDto>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        }
        catch
        {
            return new();
        }
    }

    internal static List<string> DeserializeWarnings(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new();
        }
        catch
        {
            return new();
        }
    }
}
