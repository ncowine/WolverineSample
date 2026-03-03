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

public class EnsembleVotingHandler
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task<EnsembleComputeResultDto> HandleAsync(
        ComputeEnsembleSignalsCommand command,
        MarketDataDbContext marketDb,
        IntelligenceDbContext intelDb,
        ILogger<EnsembleVotingHandler> logger)
    {
        var signalDate = command.Date ?? DateTime.UtcNow.Date;

        // Get all promoted strategies for the market
        var promotedEntries = await intelDb.TournamentEntries
            .Where(e => e.Status == TournamentStatus.Promoted && e.MarketCode == command.MarketCode)
            .ToListAsync();

        if (promotedEntries.Count == 0)
        {
            return new EnsembleComputeResultDto(
                false, command.MarketCode, signalDate, 0, 0,
                new List<EnsembleSignalDto>(), Error: "No promoted strategies found for market.");
        }

        // Get screener runs for each promoted strategy on the given date
        var strategyIds = promotedEntries.Select(e => e.StrategyId).ToList();
        var screenerRuns = await marketDb.ScreenerRuns
            .Where(r => strategyIds.Contains(r.StrategyId!.Value)
                        && r.ScanDate.Date == signalDate.Date)
            .ToListAsync();

        // Build per-symbol vote list
        var allVotes = new List<SymbolVote>();
        foreach (var run in screenerRuns)
        {
            var entry = promotedEntries.FirstOrDefault(e => e.StrategyId == run.StrategyId);
            if (entry is null) continue;

            var signals = DeserializeScreenerSignals(run.ResultsJson);
            foreach (var signal in signals)
            {
                var direction = MapDirection(signal.Direction);
                allVotes.Add(new SymbolVote(
                    signal.Symbol, entry.StrategyId, entry.StrategyName,
                    direction, entry.SharpeRatio));
            }
        }

        // Group by symbol and compute ensemble signals
        var votingMode = command.UseWeightedVoting ? "SharpeWeighted" : "Majority";
        var symbolGroups = allVotes.GroupBy(v => v.Symbol).ToList();
        var ensembleSignals = new List<EnsembleSignal>();
        var signalDtos = new List<EnsembleSignalDto>();

        foreach (var group in symbolGroups)
        {
            var votes = group.ToList();
            var result = ComputeConsensus(votes, command.MinAgreement, command.UseWeightedVoting);

            if (result is null) continue; // No consensus reached

            var voteDtos = votes.Select(v => new StrategyVoteDto(
                v.StrategyId, v.StrategyName, v.Direction.ToString(),
                v.SharpeRatio, command.UseWeightedVoting ? Math.Max(0, v.SharpeRatio) : 1m
            )).ToList();

            var entity = new EnsembleSignal
            {
                MarketCode = command.MarketCode,
                Symbol = group.Key,
                SignalDate = signalDate,
                Direction = result.Direction,
                Confidence = result.Confidence,
                VotingMode = votingMode,
                MinAgreement = command.MinAgreement,
                TotalVoters = votes.Count,
                AgreeingVoters = result.AgreeingCount,
                VotesJson = JsonSerializer.Serialize(voteDtos, JsonOpts)
            };

            ensembleSignals.Add(entity);

            signalDtos.Add(new EnsembleSignalDto(
                entity.Id, entity.MarketCode, entity.Symbol, entity.SignalDate,
                entity.Direction.ToString(), entity.Confidence, entity.VotingMode,
                entity.MinAgreement, entity.TotalVoters, entity.AgreeingVoters,
                voteDtos));
        }

        // Persist ensemble signals
        if (ensembleSignals.Count > 0)
        {
            intelDb.EnsembleSignals.AddRange(ensembleSignals);
            await intelDb.SaveChangesAsync();
        }

        logger.LogInformation(
            "Ensemble voting for {Market} on {Date}: {Symbols} symbols evaluated, {Signals} consensus signals",
            command.MarketCode, signalDate, symbolGroups.Count, ensembleSignals.Count);

        return new EnsembleComputeResultDto(
            true, command.MarketCode, signalDate,
            symbolGroups.Count, ensembleSignals.Count, signalDtos);
    }

    // ── Core voting algorithms (static, testable) ────────────────

    internal static ConsensusResult? ComputeConsensus(
        List<SymbolVote> votes, int minAgreement, bool useWeighted)
    {
        if (votes.Count == 0) return null;

        return useWeighted
            ? ComputeWeightedConsensus(votes, minAgreement)
            : ComputeMajorityConsensus(votes, minAgreement);
    }

    internal static ConsensusResult? ComputeMajorityConsensus(
        List<SymbolVote> votes, int minAgreement)
    {
        var grouped = votes
            .GroupBy(v => v.Direction)
            .Select(g => new { Direction = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .ToList();

        var winner = grouped.First();

        // Check minimum agreement
        if (winner.Count < minAgreement) return null;

        // Check for tie (two directions with same count at the top)
        if (grouped.Count > 1 && grouped[0].Count == grouped[1].Count)
            return null; // Tie — no consensus

        var confidence = (decimal)winner.Count / votes.Count;
        return new ConsensusResult(winner.Direction, confidence, winner.Count);
    }

    internal static ConsensusResult? ComputeWeightedConsensus(
        List<SymbolVote> votes, int minAgreement)
    {
        var weightedGroups = votes
            .GroupBy(v => v.Direction)
            .Select(g => new
            {
                Direction = g.Key,
                Count = g.Count(),
                TotalWeight = g.Sum(v => Math.Max(0, v.SharpeRatio))
            })
            .OrderByDescending(g => g.TotalWeight)
            .ToList();

        var winner = weightedGroups.First();

        // Still require minimum number of voters
        if (winner.Count < minAgreement) return null;

        // Check for tie in weight
        var totalWeight = weightedGroups.Sum(g => g.TotalWeight);
        if (totalWeight == 0) return null;

        if (weightedGroups.Count > 1 && winner.TotalWeight == weightedGroups[1].TotalWeight)
            return null; // Tie

        var confidence = totalWeight > 0 ? winner.TotalWeight / totalWeight : 0m;
        return new ConsensusResult(winner.Direction, confidence, winner.Count);
    }

    // ── Helpers ───────────────────────────────────────────────────

    internal static SignalType MapDirection(string direction)
    {
        return direction.ToLowerInvariant() switch
        {
            "long" or "buy" => SignalType.Buy,
            "short" or "sell" => SignalType.Sell,
            _ => SignalType.Hold
        };
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

    // ── Internal records ─────────────────────────────────────────

    internal record SymbolVote(
        string Symbol, Guid StrategyId, string StrategyName,
        SignalType Direction, decimal SharpeRatio);

    internal record ConsensusResult(SignalType Direction, decimal Confidence, int AgreeingCount);

    internal record ScreenerSignalInfo(string Symbol, string Direction);
}
