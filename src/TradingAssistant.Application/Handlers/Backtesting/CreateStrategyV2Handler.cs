using System.Text.Json;
using TradingAssistant.Contracts.Backtesting;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Domain.Backtesting;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Backtesting;

public class CreateStrategyV2Handler
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static async Task<StrategyV2Dto> HandleAsync(
        CreateStrategyV2Command command,
        BacktestDbContext db)
    {
        var strategy = new Strategy
        {
            Name = command.Name.Trim(),
            Description = command.Description?.Trim() ?? string.Empty,
            IsActive = true,
            RulesJson = JsonSerializer.Serialize(command.Definition, JsonOptions)
        };

        db.Strategies.Add(strategy);
        await db.SaveChangesAsync();

        return MapToDto(strategy, command.Definition);
    }

    internal static StrategyV2Dto MapToDto(Strategy strategy, StrategyDefinition? definition = null)
    {
        definition ??= DeserializeDefinition(strategy.RulesJson);

        return new StrategyV2Dto(
            Id: strategy.Id,
            Name: strategy.Name,
            Description: strategy.Description,
            IsActive: strategy.IsActive,
            UsesV2Engine: strategy.UsesV2Engine,
            Definition: definition,
            EntryConditionCount: definition?.EntryConditions.Sum(g => g.Conditions.Count) ?? 0,
            ExitConditionCount: definition?.ExitConditions.Sum(g => g.Conditions.Count) ?? 0,
            StopLossDescription: FormatStopLoss(definition?.StopLoss),
            TakeProfitDescription: FormatTakeProfit(definition?.TakeProfit),
            CreatedAt: strategy.CreatedAt);
    }

    internal static StrategyDefinition? DeserializeDefinition(string? rulesJson)
    {
        if (string.IsNullOrWhiteSpace(rulesJson)) return null;
        return JsonSerializer.Deserialize<StrategyDefinition>(rulesJson, JsonOptions);
    }

    private static string FormatStopLoss(StopLossConfig? sl)
    {
        if (sl is null) return "None";
        return sl.Type switch
        {
            "Atr" => $"{sl.Multiplier}x ATR",
            "FixedPercent" => $"{sl.Multiplier}%",
            "Support" => $"Support ({sl.Multiplier}x buffer)",
            _ => $"{sl.Type} ({sl.Multiplier})"
        };
    }

    private static string FormatTakeProfit(TakeProfitConfig? tp)
    {
        if (tp is null) return "None";
        return tp.Type switch
        {
            "RMultiple" => $"{tp.Multiplier}R",
            "FixedPercent" => $"{tp.Multiplier}%",
            "Resistance" => $"Resistance ({tp.Multiplier}x buffer)",
            _ => $"{tp.Type} ({tp.Multiplier})"
        };
    }
}
