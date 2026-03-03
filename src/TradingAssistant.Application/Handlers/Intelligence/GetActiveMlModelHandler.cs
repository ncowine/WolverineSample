using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Infrastructure.ML;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Intelligence;

public class GetActiveMlModelHandler
{
    public static async Task<MlModelDto?> HandleAsync(
        GetActiveMlModelQuery query,
        IntelligenceDbContext intelDb)
    {
        var model = await intelDb.MlModels
            .Where(m => m.MarketCode == query.MarketCode && m.IsActive)
            .OrderByDescending(m => m.ModelVersion)
            .FirstOrDefaultAsync();

        if (model is null)
            return null;

        var topFeatures = !string.IsNullOrEmpty(model.FeatureImportanceJson)
            ? JsonSerializer.Deserialize<List<MlModelTrainer.FeatureImportanceEntry>>(
                model.FeatureImportanceJson)?
                .Select(f => new FeatureImportanceDto(f.Name, f.Importance))
                .ToList()
            : null;

        return new MlModelDto(
            model.Id, model.MarketCode, model.ModelVersion, model.FeatureVersion,
            model.TrainedAt, model.Auc, model.Precision, model.Recall,
            model.F1Score, model.Accuracy,
            model.TrainingSamples, model.ValidationSamples,
            model.WinSamples, model.LossSamples,
            model.IsActive, model.DeactivationReason,
            topFeatures?.AsReadOnly());
    }
}
