using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Infrastructure.ML;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Intelligence;

public class GetMlModelsHandler
{
    public static async Task<IReadOnlyList<MlModelDto>> HandleAsync(
        GetMlModelsQuery query,
        IntelligenceDbContext intelDb)
    {
        var models = await intelDb.MlModels
            .Where(m => m.MarketCode == query.MarketCode)
            .OrderByDescending(m => m.ModelVersion)
            .ToListAsync();

        return models.Select(m =>
        {
            var topFeatures = !string.IsNullOrEmpty(m.FeatureImportanceJson)
                ? JsonSerializer.Deserialize<List<MlModelTrainer.FeatureImportanceEntry>>(
                    m.FeatureImportanceJson)?
                    .Select(f => new FeatureImportanceDto(f.Name, f.Importance))
                    .ToList()
                : null;

            return new MlModelDto(
                m.Id, m.MarketCode, m.ModelVersion, m.FeatureVersion,
                m.TrainedAt, m.Auc, m.Precision, m.Recall, m.F1Score, m.Accuracy,
                m.TrainingSamples, m.ValidationSamples, m.WinSamples, m.LossSamples,
                m.IsActive, m.DeactivationReason,
                topFeatures?.AsReadOnly());
        }).ToList();
    }
}
