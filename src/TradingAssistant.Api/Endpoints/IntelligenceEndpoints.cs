using Microsoft.AspNetCore.Mvc;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.SharedKernel;
using Wolverine;

namespace TradingAssistant.Api.Endpoints;

public class IntelligenceEndpoints : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/intelligence")
            .WithTags("Intelligence")
            .RequireAuthorization();

        group.MapGet("/detect-regime/{symbol}", DetectStockRegime)
            .WithSummary("Detect market regime for a stock from its price candles")
            .AllowAnonymous();

        group.MapGet("/regime/{marketCode}", GetCurrentRegime)
            .WithSummary("Get current market regime for a market");

        group.MapGet("/regime/{marketCode}/history", GetRegimeHistory)
            .WithSummary("Get paginated regime classification history");

        group.MapGet("/breadth/{marketCode}", GetLatestBreadth)
            .WithSummary("Get latest breadth snapshot for a market");

        group.MapGet("/correlations", GetCorrelationMatrix)
            .WithSummary("Get latest cross-market correlation matrix");

        group.MapGet("/market-profile/{marketCode}", GetMarketProfile)
            .WithSummary("Get market DNA profile");

        group.MapPost("/autopsy", RunAutopsy)
            .WithSummary("Run Claude-powered post-mortem on a losing strategy month");

        group.MapGet("/autopsy/{strategyId:guid}", GetAutopsyHistory)
            .WithSummary("Get autopsy history for a strategy");

        group.MapPost("/discover-rules", DiscoverRules)
            .WithSummary("Analyze trade history to discover patterns distinguishing winners from losers");

        group.MapGet("/discovered-rules/{strategyId:guid}", GetDiscoveredRules)
            .WithSummary("Get rule discovery history for a strategy");

        group.MapGet("/features", GetFeatureSnapshots)
            .WithSummary("Get paginated feature snapshots with optional filters");

        group.MapGet("/training-data", GetTrainingData)
            .WithSummary("Get training dataset metadata and feature column schema");

        group.MapPost("/ml/retrain", RetrainModel)
            .WithSummary("Train/retrain ML model for a market using labeled feature snapshots");

        group.MapPost("/ml/retrain/{marketCode}", RetrainModelByMarket)
            .WithSummary("Manually trigger ML model retraining for a specific market");

        group.MapGet("/ml/models/{marketCode}", GetMlModels)
            .WithSummary("Get ML model metadata for a market");

        group.MapGet("/ml/models/{marketCode}/active", GetActiveMlModel)
            .WithSummary("Get the active ML model for a market");

        group.MapGet("/ml/feature-importance/{marketCode}", GetMlFeatureImportance)
            .WithSummary("Get top feature importance for active ML model");

        group.MapPost("/ml/predict", PredictConfidence)
            .WithSummary("Run ML prediction for a symbol in a market");
    }

    private static async Task<StockRegimeDto> DetectStockRegime(
        [FromRoute] string symbol, IMessageBus bus)
    {
        return await bus.InvokeAsync<StockRegimeDto>(
            new DetectStockRegimeQuery(symbol));
    }

    private static async Task<MarketRegimeDto> GetCurrentRegime(
        [FromRoute] string marketCode, IMessageBus bus)
    {
        return await bus.InvokeAsync<MarketRegimeDto>(
            new GetCurrentRegimeQuery(marketCode));
    }

    private static async Task<PagedResponse<MarketRegimeDto>> GetRegimeHistory(
        [FromRoute] string marketCode,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        IMessageBus bus)
    {
        return await bus.InvokeAsync<PagedResponse<MarketRegimeDto>>(
            new GetRegimeHistoryQuery(marketCode, page > 0 ? page : 1, pageSize > 0 ? pageSize : 20));
    }

    private static async Task<BreadthSnapshotDto> GetLatestBreadth(
        [FromRoute] string marketCode, IMessageBus bus)
    {
        return await bus.InvokeAsync<BreadthSnapshotDto>(
            new GetLatestBreadthQuery(marketCode));
    }

    private static async Task<CorrelationMatrixDto> GetCorrelationMatrix(IMessageBus bus)
    {
        return await bus.InvokeAsync<CorrelationMatrixDto>(
            new GetCorrelationMatrixQuery());
    }

    private static async Task<MarketProfileDto> GetMarketProfile(
        [FromRoute] string marketCode, IMessageBus bus)
    {
        return await bus.InvokeAsync<MarketProfileDto>(
            new GetMarketProfileQuery(marketCode));
    }

    private static async Task<StrategyAutopsyResultDto> RunAutopsy(
        [FromBody] RunAutopsyCommand command, IMessageBus bus)
    {
        return await bus.InvokeAsync<StrategyAutopsyResultDto>(command);
    }

    private static async Task<IReadOnlyList<StrategyAutopsyDto>> GetAutopsyHistory(
        [FromRoute] Guid strategyId, IMessageBus bus)
    {
        return await bus.InvokeAsync<IReadOnlyList<StrategyAutopsyDto>>(
            new GetAutopsyHistoryQuery(strategyId));
    }

    private static async Task<DiscoverRulesResultDto> DiscoverRules(
        [FromBody] DiscoverRulesCommand command, IMessageBus bus)
    {
        return await bus.InvokeAsync<DiscoverRulesResultDto>(command);
    }

    private static async Task<IReadOnlyList<DiscoverRulesResultDto>> GetDiscoveredRules(
        [FromRoute] Guid strategyId, IMessageBus bus)
    {
        return await bus.InvokeAsync<IReadOnlyList<DiscoverRulesResultDto>>(
            new GetDiscoveredRulesQuery(strategyId));
    }

    private static async Task<PagedResponse<FeatureSnapshotDto>> GetFeatureSnapshots(
        [FromQuery] string? symbol,
        [FromQuery] string? marketCode,
        [FromQuery] string? outcome,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        IMessageBus bus)
    {
        return await bus.InvokeAsync<PagedResponse<FeatureSnapshotDto>>(
            new GetFeatureSnapshotsQuery(symbol, marketCode, outcome,
                page > 0 ? page : 1, pageSize > 0 ? pageSize : 50));
    }

    private static async Task<TrainingDataResultDto> GetTrainingData(
        [FromQuery] string? symbol,
        [FromQuery] string? marketCode,
        [FromQuery] int? minVersion,
        [FromQuery] int maxRecords,
        IMessageBus bus)
    {
        return await bus.InvokeAsync<TrainingDataResultDto>(
            new GetTrainingDataQuery(symbol, marketCode, minVersion,
                maxRecords > 0 ? maxRecords : 10_000));
    }

    private static async Task<RetrainResultDto> RetrainModel(
        [FromBody] RetrainModelCommand command, IMessageBus bus)
    {
        return await bus.InvokeAsync<RetrainResultDto>(command);
    }

    private static async Task<RetrainResultDto> RetrainModelByMarket(
        [FromRoute] string marketCode, IMessageBus bus)
    {
        return await bus.InvokeAsync<RetrainResultDto>(
            new RetrainModelCommand(marketCode));
    }

    private static async Task<IReadOnlyList<MlModelDto>> GetMlModels(
        [FromRoute] string marketCode, IMessageBus bus)
    {
        return await bus.InvokeAsync<IReadOnlyList<MlModelDto>>(
            new GetMlModelsQuery(marketCode));
    }

    private static async Task<MlModelDto?> GetActiveMlModel(
        [FromRoute] string marketCode, IMessageBus bus)
    {
        return await bus.InvokeAsync<MlModelDto?>(
            new GetActiveMlModelQuery(marketCode));
    }

    private static async Task<IReadOnlyList<FeatureImportanceDto>> GetMlFeatureImportance(
        [FromRoute] string marketCode, IMessageBus bus)
    {
        return await bus.InvokeAsync<IReadOnlyList<FeatureImportanceDto>>(
            new GetMlFeatureImportanceQuery(marketCode));
    }

    private static async Task<MlPredictionResultDto> PredictConfidence(
        [FromBody] PredictConfidenceCommand command, IMessageBus bus)
    {
        return await bus.InvokeAsync<MlPredictionResultDto>(command);
    }
}
