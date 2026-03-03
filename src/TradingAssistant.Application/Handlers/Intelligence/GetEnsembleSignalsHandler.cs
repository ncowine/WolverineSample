using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Intelligence;

public class GetEnsembleSignalsHandler
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task<IReadOnlyList<EnsembleSignalDto>> HandleAsync(
        GetEnsembleSignalsQuery query,
        IntelligenceDbContext db)
    {
        var q = db.EnsembleSignals
            .Where(s => s.MarketCode == query.MarketCode);

        if (query.Date.HasValue)
        {
            q = q.Where(s => s.SignalDate.Date == query.Date.Value.Date);
        }
        else
        {
            // Default to latest date
            var latestDate = await db.EnsembleSignals
                .Where(s => s.MarketCode == query.MarketCode)
                .OrderByDescending(s => s.SignalDate)
                .Select(s => s.SignalDate)
                .FirstOrDefaultAsync();

            if (latestDate == default)
                return new List<EnsembleSignalDto>();

            q = q.Where(s => s.SignalDate.Date == latestDate.Date);
        }

        var signals = await q.OrderByDescending(s => s.Confidence).ToListAsync();

        return signals.Select(s =>
        {
            var votes = DeserializeVotes(s.VotesJson);
            return new EnsembleSignalDto(
                s.Id, s.MarketCode, s.Symbol, s.SignalDate,
                s.Direction.ToString(), s.Confidence, s.VotingMode,
                s.MinAgreement, s.TotalVoters, s.AgreeingVoters, votes);
        }).ToList();
    }

    internal static IReadOnlyList<StrategyVoteDto> DeserializeVotes(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<StrategyVoteDto>>(json, JsonOpts)
                   ?? new List<StrategyVoteDto>();
        }
        catch
        {
            return new List<StrategyVoteDto>();
        }
    }
}
