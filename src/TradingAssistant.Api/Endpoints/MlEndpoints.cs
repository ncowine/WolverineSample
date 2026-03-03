using Microsoft.AspNetCore.Mvc;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using Wolverine;

namespace TradingAssistant.Api.Endpoints;

public class MlEndpoints : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ml")
            .WithTags("ML Models")
            .RequireAuthorization();

        group.MapGet("/models", GetModelRegistry)
            .WithSummary("List all ML models across all markets (model registry)");

        group.MapGet("/models/{marketCode}", GetModels)
            .WithSummary("Get ML model versions for a specific market");

        group.MapGet("/models/{marketCode}/active", GetActiveModel)
            .WithSummary("Get the currently active ML model for a market");

        group.MapGet("/feature-importance/{marketCode}", GetFeatureImportance)
            .WithSummary("Get top feature importance scores from the active model");

        group.MapPost("/retrain", RetrainModel)
            .WithSummary("Manually trigger ML model retraining for a market");

        group.MapPost("/predict", Predict)
            .WithSummary("Run ML confidence prediction for a symbol");
    }

    private static async Task<IReadOnlyList<MlModelDto>> GetModelRegistry(
        [FromQuery] string? marketCode,
        IMessageBus bus)
    {
        return await bus.InvokeAsync<IReadOnlyList<MlModelDto>>(
            new GetModelRegistryQuery(marketCode));
    }

    private static async Task<IReadOnlyList<MlModelDto>> GetModels(
        [FromRoute] string marketCode, IMessageBus bus)
    {
        return await bus.InvokeAsync<IReadOnlyList<MlModelDto>>(
            new GetMlModelsQuery(marketCode));
    }

    private static async Task<IResult> GetActiveModel(
        [FromRoute] string marketCode, IMessageBus bus)
    {
        var result = await bus.InvokeAsync<MlModelDto?>(
            new GetActiveMlModelQuery(marketCode));

        return result is null
            ? Results.NotFound($"No active ML model for market '{marketCode}'.")
            : Results.Ok(result);
    }

    private static async Task<IReadOnlyList<FeatureImportanceDto>> GetFeatureImportance(
        [FromRoute] string marketCode, IMessageBus bus)
    {
        return await bus.InvokeAsync<IReadOnlyList<FeatureImportanceDto>>(
            new GetMlFeatureImportanceQuery(marketCode));
    }

    private static async Task<RetrainResultDto> RetrainModel(
        [FromBody] RetrainModelCommand command, IMessageBus bus)
    {
        return await bus.InvokeAsync<RetrainResultDto>(command);
    }

    private static async Task<MlPredictionResultDto> Predict(
        [FromBody] PredictConfidenceCommand command, IMessageBus bus)
    {
        return await bus.InvokeAsync<MlPredictionResultDto>(command);
    }
}
