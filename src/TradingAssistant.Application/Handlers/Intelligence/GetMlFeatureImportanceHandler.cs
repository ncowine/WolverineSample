using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Infrastructure.ML;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Intelligence;

public class GetMlFeatureImportanceHandler
{
    public static async Task<IReadOnlyList<FeatureImportanceDto>> HandleAsync(
        GetMlFeatureImportanceQuery query,
        IntelligenceDbContext intelDb)
    {
        var model = await intelDb.MlModels
            .Where(m => m.MarketCode == query.MarketCode && m.IsActive)
            .OrderByDescending(m => m.ModelVersion)
            .FirstOrDefaultAsync();

        if (model is null || string.IsNullOrEmpty(model.FeatureImportanceJson))
            return Array.Empty<FeatureImportanceDto>();

        var entries = JsonSerializer
            .Deserialize<List<MlModelTrainer.FeatureImportanceEntry>>(model.FeatureImportanceJson);

        return entries?
            .Select(f => new FeatureImportanceDto(f.Name, f.Importance))
            .ToList()
            .AsReadOnly() ?? (IReadOnlyList<FeatureImportanceDto>)Array.Empty<FeatureImportanceDto>();
    }
}
