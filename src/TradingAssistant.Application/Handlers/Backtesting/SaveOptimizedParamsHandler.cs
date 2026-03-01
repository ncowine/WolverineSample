using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TradingAssistant.Application.Backtesting;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Domain.Backtesting;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Backtesting;

public class SaveOptimizedParamsHandler
{
    /// <summary>
    /// Maximum parameter set versions to keep per strategy.
    /// Older versions beyond this limit are permanently deleted.
    /// </summary>
    internal const int MaxVersionsPerStrategy = 5;

    /// <summary>
    /// Persist "blessed" parameters from a walk-forward result.
    /// Deactivates previous active set, assigns version number, and purges old versions beyond limit.
    /// </summary>
    public static async Task<OptimizedParameterSetDto> HandleAsync(
        Guid strategyId,
        WalkForwardResult walkForwardResult,
        BacktestDbContext db)
    {
        // Deactivate previous active set for this strategy
        var previousActive = await db.OptimizedParameterSets
            .Where(p => p.StrategyId == strategyId && p.IsActive)
            .ToListAsync();

        foreach (var prev in previousActive)
            prev.IsActive = false;

        // Determine next version number
        var maxVersion = await db.OptimizedParameterSets
            .Where(p => p.StrategyId == strategyId)
            .Select(p => (int?)p.Version)
            .MaxAsync() ?? 0;

        var entity = new OptimizedParameterSet
        {
            Id = Guid.NewGuid(),
            StrategyId = strategyId,
            ParametersJson = JsonSerializer.Serialize(walkForwardResult.BlessedParameters.Values),
            AvgOutOfSampleSharpe = walkForwardResult.AverageOutOfSampleSharpe,
            AvgEfficiency = walkForwardResult.AverageEfficiency,
            AvgOverfittingScore = walkForwardResult.AverageOverfittingScore,
            OverfittingGrade = walkForwardResult.Grade.ToString(),
            WindowCount = walkForwardResult.Windows.Count,
            Version = maxVersion + 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        db.OptimizedParameterSets.Add(entity);

        // Purge old versions beyond the limit
        var allVersions = await db.OptimizedParameterSets
            .Where(p => p.StrategyId == strategyId && p.Id != entity.Id)
            .OrderByDescending(p => p.Version)
            .ToListAsync();

        // Keep MaxVersionsPerStrategy - 1 old ones (plus the new one = MaxVersionsPerStrategy total)
        var toRemove = allVersions.Skip(MaxVersionsPerStrategy - 1).ToList();
        if (toRemove.Count > 0)
            db.OptimizedParameterSets.RemoveRange(toRemove);

        await db.SaveChangesAsync();

        return MapToDto(entity);
    }

    internal static OptimizedParameterSetDto MapToDto(OptimizedParameterSet entity)
    {
        var parameters = JsonSerializer.Deserialize<Dictionary<string, decimal>>(entity.ParametersJson)
                         ?? new Dictionary<string, decimal>();

        return new OptimizedParameterSetDto(
            entity.Id,
            entity.StrategyId,
            parameters,
            entity.AvgOutOfSampleSharpe,
            entity.AvgEfficiency,
            entity.AvgOverfittingScore,
            entity.OverfittingGrade,
            entity.WindowCount,
            entity.Version,
            entity.IsActive,
            entity.CreatedAt);
    }
}
