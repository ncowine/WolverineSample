using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Events;
using TradingAssistant.Domain.Backtesting;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Backtesting;

public class CreateStrategyHandler
{
    public static async Task<(StrategyCreated, StrategyDto)> HandleAsync(
        CreateStrategyCommand command,
        BacktestDbContext db)
    {
        var strategy = new Strategy
        {
            Name = command.Name,
            Description = command.Description,
            IsActive = true
        };

        foreach (var ruleDto in command.Rules)
        {
            if (!Enum.TryParse<IndicatorType>(ruleDto.IndicatorType, true, out var indicatorType))
                throw new InvalidOperationException($"Invalid indicator type: {ruleDto.IndicatorType}");

            if (!Enum.TryParse<SignalType>(ruleDto.SignalType, true, out var signalType))
                throw new InvalidOperationException($"Invalid signal type: {ruleDto.SignalType}");

            strategy.Rules.Add(new StrategyRule
            {
                StrategyId = strategy.Id,
                IndicatorType = indicatorType,
                Condition = ruleDto.Condition,
                Threshold = ruleDto.Threshold,
                SignalType = signalType
            });
        }

        db.Strategies.Add(strategy);
        await db.SaveChangesAsync();

        var dto = new StrategyDto(
            strategy.Id, strategy.Name, strategy.Description, strategy.IsActive,
            command.Rules, strategy.CreatedAt);

        return (new StrategyCreated(strategy.Id, strategy.Name), dto);
    }
}
