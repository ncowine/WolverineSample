using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Events;
using TradingAssistant.Domain.Intelligence;
using TradingAssistant.Domain.Intelligence.Enums;
using TradingAssistant.Infrastructure.ML;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Intelligence;

/// <summary>
/// Loads labeled FeatureSnapshots, trains a LightGBM binary classification model,
/// saves the model to disk, and persists metadata to IntelligenceDbContext.
///
/// Auto-rollback: if new model AUC &lt; previous active model AUC, the old model stays active.
/// Publishes ModelTrained event on successful training via tuple cascade.
/// </summary>
public class RetrainModelHandler
{
    internal const string ModelBasePath = "data/models";

    public static async Task<(RetrainResultDto, ModelTrained)> HandleAsync(
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
            var failEvent = new ModelTrained(command.MarketCode, 0, 0, false,
                result.FailureReason, DateTime.UtcNow);
            return (new RetrainResultDto(false, result.FailureReason, null), failEvent);
        }

        // Feature drift detection (compare training window vs most recent 20%)
        FeatureDriftResultDto? driftDto = null;
        if (vectors.Count >= 50)
        {
            var recentCount = Math.Max(10, vectors.Count / 5);
            var trainingWindow = vectors.Take(vectors.Count - recentCount).ToList();
            var recentWindow = vectors.Skip(vectors.Count - recentCount).ToList();
            var driftReport = MlModelTrainer.ComputeFeatureDrift(trainingWindow, recentWindow);

            if (driftReport.HasSignificantDrift)
            {
                logger.LogWarning(
                    "Feature drift detected for {MarketCode}: {Count} features shifted significantly",
                    command.MarketCode, driftReport.SignificantEntries.Count);
            }

            driftDto = new FeatureDriftResultDto(
                driftReport.Entries.Select(e => new FeatureDriftEntry(
                    e.FeatureName, e.TrainingMean, e.RecentMean,
                    e.TrainingStdDev, e.RecentStdDev,
                    e.DriftMagnitude, e.IsSignificant)).ToList(),
                driftReport.HasSignificantDrift,
                driftReport.TrainingWindowSize,
                driftReport.RecentWindowSize);
        }

        // Determine next version
        var latestVersion = await intelDb.MlModels
            .Where(m => m.MarketCode == command.MarketCode)
            .MaxAsync(m => (int?)m.ModelVersion) ?? 0;
        var nextVersion = latestVersion + 1;

        // Save model file
        var modelPath = Path.Combine(ModelBasePath, command.MarketCode, $"v{nextVersion}.zip");
        trainer.SaveModel(result, modelPath);

        // Auto-rollback: compare against previous active model's AUC
        var previousActive = await intelDb.MlModels
            .Where(m => m.MarketCode == command.MarketCode && m.IsActive)
            .OrderByDescending(m => m.ModelVersion)
            .FirstOrDefaultAsync();

        var meetsMinimum = result.MeetsMinimumAuc;
        string? rollbackReason = null;

        if (!meetsMinimum)
        {
            rollbackReason = $"AUC {result.Auc:F4} below minimum threshold {MlModelTrainer.MinimumAuc}";
        }
        else if (previousActive is not null && result.Auc < previousActive.Auc)
        {
            // Auto-rollback: new model is worse than current active
            rollbackReason = $"AUC {result.Auc:F4} < previous active v{previousActive.ModelVersion} AUC {previousActive.Auc:F4}";
            meetsMinimum = false; // Don't activate
        }

        var isActive = meetsMinimum;

        if (isActive && previousActive is not null)
        {
            // Deactivate previous active models
            var activeModels = await intelDb.MlModels
                .Where(m => m.MarketCode == command.MarketCode && m.IsActive)
                .ToListAsync();

            foreach (var m in activeModels)
            {
                m.IsActive = false;
                m.DeactivationReason = $"Superseded by v{nextVersion}";
            }
        }

        if (rollbackReason is not null)
        {
            logger.LogWarning(
                "ML model v{Version} for {MarketCode} rolled back: {Reason}",
                nextVersion, command.MarketCode, rollbackReason);
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
            DeactivationReason = isActive ? null : rollbackReason
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

        var modelTrainedEvent = new ModelTrained(
            command.MarketCode, nextVersion, result.Auc,
            isActive, rollbackReason, mlModel.TrainedAt);

        return (new RetrainResultDto(true, null, dto, rollbackReason, driftDto), modelTrainedEvent);
    }
}
