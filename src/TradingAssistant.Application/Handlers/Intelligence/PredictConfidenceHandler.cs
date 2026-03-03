using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Domain.Intelligence.Enums;
using TradingAssistant.Infrastructure.ML;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Intelligence;

/// <summary>
/// Runs ML inference using the in-memory MlPredictionService.
/// Loads the active model on first request if not already loaded.
/// </summary>
public class PredictConfidenceHandler
{
    public static async Task<MlPredictionResultDto> HandleAsync(
        PredictConfidenceCommand command,
        MlPredictionService predictionService,
        IntelligenceDbContext intelDb,
        ILogger<PredictConfidenceHandler> logger)
    {
        // Ensure model is loaded
        if (!predictionService.HasModel(command.MarketCode))
        {
            var activeModel = await intelDb.MlModels
                .Where(m => m.MarketCode == command.MarketCode && m.IsActive)
                .OrderByDescending(m => m.ModelVersion)
                .FirstOrDefaultAsync();

            if (activeModel is null || !File.Exists(activeModel.ModelPath))
            {
                return new MlPredictionResultDto(
                    command.MarketCode, command.Symbol,
                    null, false, null, null);
            }

            predictionService.LoadModel(command.MarketCode, activeModel.ModelPath);
            logger.LogInformation(
                "Loaded ML model v{Version} for {Market} from {Path}",
                activeModel.ModelVersion, command.MarketCode, activeModel.ModelPath);
        }

        // Get latest feature snapshot for this symbol
        var snapshot = await intelDb.FeatureSnapshots
            .Where(s => s.Symbol == command.Symbol && s.MarketCode == command.MarketCode)
            .OrderByDescending(s => s.CapturedAt)
            .FirstOrDefaultAsync();

        if (snapshot is null)
        {
            logger.LogWarning(
                "No feature snapshot found for {Symbol} in {Market}",
                command.Symbol, command.MarketCode);

            return new MlPredictionResultDto(
                command.MarketCode, command.Symbol,
                null, true,
                predictionService.GetLoadedModelPath(command.MarketCode), null);
        }

        var vector = FeatureVectorConverter.FromSnapshot(snapshot);
        if (vector is null)
        {
            return new MlPredictionResultDto(
                command.MarketCode, command.Symbol,
                null, true,
                predictionService.GetLoadedModelPath(command.MarketCode), null);
        }

        var confidence = predictionService.PredictConfidence(command.MarketCode, vector);

        // Get active model version for the response
        var modelVersion = await intelDb.MlModels
            .Where(m => m.MarketCode == command.MarketCode && m.IsActive)
            .Select(m => (int?)m.ModelVersion)
            .FirstOrDefaultAsync();

        return new MlPredictionResultDto(
            command.MarketCode, command.Symbol,
            confidence, true,
            predictionService.GetLoadedModelPath(command.MarketCode),
            modelVersion);
    }
}
