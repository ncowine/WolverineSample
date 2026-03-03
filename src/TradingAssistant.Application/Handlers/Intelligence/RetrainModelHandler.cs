using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Domain.Intelligence;
using TradingAssistant.Domain.Intelligence.Enums;
using TradingAssistant.Infrastructure.ML;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Intelligence;

/// <summary>
/// Loads labeled FeatureSnapshots, trains a LightGBM binary classification model,
/// saves the model to disk, and persists metadata to IntelligenceDbContext.
/// </summary>
public class RetrainModelHandler
{
    internal const string ModelBasePath = "data/models";

    public static async Task<RetrainResultDto> HandleAsync(
        RetrainModelCommand command,
        IntelligenceDbContext intelDb,
        ILogger<RetrainModelHandler> logger)
    {
        // Load labeled feature snapshots
        var query = intelDb.FeatureSnapshots
            .Where(s => s.TradeOutcome != TradeOutcome.Pending
                        && s.MarketCode == command.MarketCode);

        if (command.MinFeatureVersion.HasValue)
            query = query.Where(s => s.FeatureVersion >= command.MinFeatureVersion.Value);

        var snapshots = await query
            .OrderBy(s => s.CapturedAt)
            .Take(command.MaxSamples)
            .ToListAsync();

        // Batch convert to typed FeatureVectors
        var vectors = FeatureVectorConverter.BatchConvert(snapshots);

        logger.LogInformation(
            "Training ML model for {MarketCode}: {Count} samples ({Win}W/{Loss}L)",
            command.MarketCode, vectors.Count,
            vectors.Count(v => v.Label), vectors.Count(v => !v.Label));

        // Train model
        var trainer = new MlModelTrainer();
        var result = trainer.Train(vectors, command.TrainSplitRatio);

        if (!result.Success)
        {
            logger.LogWarning("Training failed for {MarketCode}: {Reason}",
                command.MarketCode, result.FailureReason);
            return new RetrainResultDto(false, result.FailureReason, null);
        }

        // Determine next version
        var latestVersion = await intelDb.MlModels
            .Where(m => m.MarketCode == command.MarketCode)
            .MaxAsync(m => (int?)m.ModelVersion) ?? 0;
        var nextVersion = latestVersion + 1;

        // Save model file
        var modelPath = Path.Combine(ModelBasePath, command.MarketCode, $"v{nextVersion}.zip");
        trainer.SaveModel(result, modelPath);

        // Deactivate previous active models if new model meets AUC threshold
        var isActive = result.MeetsMinimumAuc;

        if (isActive)
        {
            var activeModels = await intelDb.MlModels
                .Where(m => m.MarketCode == command.MarketCode && m.IsActive)
                .ToListAsync();

            foreach (var m in activeModels)
            {
                m.IsActive = false;
                m.DeactivationReason = $"Superseded by v{nextVersion}";
            }
        }

        // Persist model metadata
        var mlModel = new MlModel
        {
            MarketCode = command.MarketCode,
            ModelVersion = nextVersion,
            FeatureVersion = FeatureExtractor.CurrentVersion,
            ModelPath = modelPath,
            TrainedAt = DateTime.UtcNow,
            Auc = result.Auc,
            Precision = result.Precision,
            Recall = result.Recall,
            F1Score = result.F1Score,
            Accuracy = result.Accuracy,
            TrainingSamples = result.TrainingSamples,
            ValidationSamples = result.ValidationSamples,
            WinSamples = result.WinSamples,
            LossSamples = result.LossSamples,
            FeatureImportanceJson = result.TopFeaturesJson,
            IsActive = isActive,
            DeactivationReason = isActive ? null : $"AUC {result.Auc:F4} below minimum {MlModelTrainer.MinimumAuc}"
        };

        intelDb.MlModels.Add(mlModel);
        await intelDb.SaveChangesAsync();

        logger.LogInformation(
            "ML model v{Version} for {MarketCode}: AUC={Auc:F4}, Active={Active}",
            nextVersion, command.MarketCode, result.Auc, isActive);

        var topFeatures = result.TopFeatures
            .Select(f => new FeatureImportanceDto(f.Name, f.Importance))
            .ToList();

        var dto = new MlModelDto(
            mlModel.Id, mlModel.MarketCode, mlModel.ModelVersion, mlModel.FeatureVersion,
            mlModel.TrainedAt, mlModel.Auc, mlModel.Precision, mlModel.Recall,
            mlModel.F1Score, mlModel.Accuracy,
            mlModel.TrainingSamples, mlModel.ValidationSamples,
            mlModel.WinSamples, mlModel.LossSamples,
            mlModel.IsActive, mlModel.DeactivationReason, topFeatures);

        return new RetrainResultDto(true, null, dto);
    }
}
