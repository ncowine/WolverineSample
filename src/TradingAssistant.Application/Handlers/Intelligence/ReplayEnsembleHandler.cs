using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Domain.Intelligence;
using TradingAssistant.Domain.Intelligence.Enums;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Intelligence;

public class ReplayEnsembleHandler
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task<EnsembleReplayResultDto> HandleAsync(
        ReplayEnsembleCommand command,
        MarketDataDbContext marketDb,
        IntelligenceDbContext intelDb,
        ILogger<ReplayEnsembleHandler> logger)
    {
        // Get all promoted strategies for the market
        var promotedEntries = await intelDb.TournamentEntries
            .Where(e => e.Status == TournamentStatus.Promoted && e.MarketCode == command.MarketCode)
            .ToListAsync();

        if (promotedEntries.Count == 0)
        {
            return new EnsembleReplayResultDto(
                command.MarketCode, command.StartDate, command.EndDate,
                0, 0, 0, 0, new List<EnsembleSignalDto>());
        }

        // Get all screener runs in date range for promoted strategies
        var strategyIds = promotedEntries.Select(e => e.StrategyId).ToList();
        var screenerRuns = await marketDb.ScreenerRuns
            .Where(r => strategyIds.Contains(r.StrategyId!.Value)
                        && r.ScanDate.Date >= command.StartDate.Date
                        && r.ScanDate.Date <= command.EndDate.Date)
            .ToListAsync();

        // Group screener runs by date
        var runsByDate = screenerRuns
            .GroupBy(r => r.ScanDate.Date)
            .OrderBy(g => g.Key)
            .ToList();

        var allSignalDtos = new List<EnsembleSignalDto>();

        foreach (var dateGroup in runsByDate)
        {
            var date = dateGroup.Key;

            // Build votes for this date
            var votes = new List<EnsembleVotingHandler.SymbolVote>();
            foreach (var run in dateGroup)
            {
                var entry = promotedEntries.FirstOrDefault(e => e.StrategyId == run.StrategyId);
                if (entry is null) continue;

                var signals = DeserializeScreenerSignals(run.ResultsJson);
                foreach (var signal in signals)
                {
                    var direction = EnsembleVotingHandler.MapDirection(signal.Direction);
                    votes.Add(new EnsembleVotingHandler.SymbolVote(
                        signal.Symbol, entry.StrategyId, entry.StrategyName,
                        direction, entry.SharpeRatio));
                }
            }

            // Group by symbol and compute consensus
            var symbolGroups = votes.GroupBy(v => v.Symbol);
            foreach (var group in symbolGroups)
            {
                var symbolVotes = group.ToList();
                var result = EnsembleVotingHandler.ComputeConsensus(
                    symbolVotes, command.MinAgreement, command.UseWeightedVoting);

                if (result is null) continue;

                var votingMode = command.UseWeightedVoting ? "SharpeWeighted" : "Majority";
                var voteDtos = symbolVotes.Select(v => new StrategyVoteDto(
                    v.StrategyId, v.StrategyName, v.Direction.ToString(),
                    v.SharpeRatio, command.UseWeightedVoting ? Math.Max(0, v.SharpeRatio) : 1m
                )).ToList();

                allSignalDtos.Add(new EnsembleSignalDto(
                    Guid.NewGuid(), command.MarketCode, group.Key, date,
                    result.Direction.ToString(), result.Confidence,
                    votingMode, command.MinAgreement, symbolVotes.Count,
                    result.AgreeingCount, voteDtos));
            }
        }

        logger.LogInformation(
            "Ensemble replay for {Market} ({Start} to {End}): {Days} days, {Signals} signals",
            command.MarketCode, command.StartDate, command.EndDate,
            runsByDate.Count, allSignalDtos.Count);

        return new EnsembleReplayResultDto(
            command.MarketCode, command.StartDate, command.EndDate,
            runsByDate.Count, allSignalDtos.Count,
            allSignalDtos.Count(s => s.Direction == "Buy"),
            allSignalDtos.Count(s => s.Direction == "Sell"),
            allSignalDtos);
    }

    private static List<ScreenerSignalInfo> DeserializeScreenerSignals(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<ScreenerSignalInfo>>(json, JsonOpts)
                   ?? new List<ScreenerSignalInfo>();
        }
        catch
        {
            return new List<ScreenerSignalInfo>();
        }
    }

    internal record ScreenerSignalInfo(string Symbol, string Direction);
}
