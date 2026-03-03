using Microsoft.EntityFrameworkCore;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Domain.Intelligence.Enums;
using TradingAssistant.Infrastructure.ML;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Intelligence;

/// <summary>
/// Loads labeled FeatureSnapshots and batch-converts to typed FeatureVectors
/// for ML model training. Returns metadata about the training dataset.
/// </summary>
public class GetTrainingDataHandler
{
    public static async Task<TrainingDataResultDto> HandleAsync(
        GetTrainingDataQuery query,
        IntelligenceDbContext intelDb)
    {
        var dbQuery = intelDb.FeatureSnapshots
            .Where(s => s.TradeOutcome != TradeOutcome.Pending);

        if (!string.IsNullOrEmpty(query.Symbol))
            dbQuery = dbQuery.Where(s => s.Symbol == query.Symbol);

        if (!string.IsNullOrEmpty(query.MarketCode))
            dbQuery = dbQuery.Where(s => s.MarketCode == query.MarketCode);

        if (query.MinFeatureVersion.HasValue)
            dbQuery = dbQuery.Where(s => s.FeatureVersion >= query.MinFeatureVersion.Value);

        var snapshots = await dbQuery
            .OrderByDescending(s => s.CapturedAt)
            .Take(query.MaxRecords)
            .ToListAsync();

        var vectors = FeatureVectorConverter.BatchConvert(snapshots);

        return new TrainingDataResultDto(
            TotalSnapshots: snapshots.Count,
            ConvertedVectors: vectors.Count,
            WinCount: vectors.Count(v => v.Label),
            LossCount: vectors.Count(v => !v.Label),
            SkippedPending: 0,
            FeatureVersion: FeatureExtractor.CurrentVersion,
            FeatureColumns: FeatureVector.GetFeatureColumnNames());
    }
}
