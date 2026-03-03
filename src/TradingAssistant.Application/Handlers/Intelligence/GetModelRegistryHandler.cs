using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Infrastructure.ML;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Intelligence;

public class GetModelRegistryHandler
{
    public static async Task<IReadOnlyList<MlModelDto>> HandleAsync(
        GetModelRegistryQuery query,
        IntelligenceDbContext intelDb)
    {
        var modelsQuery = intelDb.MlModels.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.MarketCode))
            modelsQuery = modelsQuery.Where(m => m.MarketCode == query.MarketCode);

        var models = await modelsQuery
            .OrderBy(m => m.MarketCode)
            .ThenByDescending(m => m.ModelVersion)
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
