using FluentValidation;
using TradingAssistant.Contracts.Commands;

namespace TradingAssistant.Application.Validators;

public class CreateStrategyV2CommandValidator : AbstractValidator<CreateStrategyV2Command>
{
    public CreateStrategyV2CommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Strategy name is required.")
            .MaximumLength(200);

        RuleFor(x => x.Definition)
            .NotNull().WithMessage("Strategy definition is required.");

        When(x => x.Definition != null, () =>
        {
            RuleFor(x => x.Definition.EntryConditions)
                .NotEmpty().WithMessage("At least one entry condition group is required.");

            RuleFor(x => x.Definition.EntryConditions)
                .Must(groups => groups.All(g => g.Conditions.Count > 0))
                .WithMessage("Each entry condition group must have at least one condition.");

            RuleFor(x => x.Definition.StopLoss)
                .NotNull().WithMessage("Stop loss configuration is required.");

            When(x => x.Definition.StopLoss != null, () =>
            {
                RuleFor(x => x.Definition.StopLoss.Multiplier)
                    .GreaterThan(0).WithMessage("Stop loss multiplier must be positive.");
            });

            When(x => x.Definition.TakeProfit != null, () =>
            {
                RuleFor(x => x.Definition.TakeProfit.Multiplier)
                    .GreaterThan(0).WithMessage("Take profit multiplier must be positive.");
            });

            When(x => x.Definition.PositionSizing != null, () =>
            {
                RuleFor(x => x.Definition.PositionSizing.RiskPercent)
                    .InclusiveBetween(0.1m, 10m)
                    .WithMessage("Risk percent must be between 0.1% and 10%.");

                RuleFor(x => x.Definition.PositionSizing.MaxPositions)
                    .InclusiveBetween(1, 50)
                    .WithMessage("Max positions must be between 1 and 50.");

                RuleFor(x => x.Definition.PositionSizing.MaxDrawdownPercent)
                    .InclusiveBetween(5m, 50m)
                    .WithMessage("Max drawdown must be between 5% and 50%.");
            });
        });
    }
}
