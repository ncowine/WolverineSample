using FluentValidation;
using TradingAssistant.Contracts.Commands;

namespace TradingAssistant.Application.Validators;

public class CreateStrategyCommandValidator : AbstractValidator<CreateStrategyCommand>
{
    public CreateStrategyCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).WithMessage("Name is required (max 200 chars).");
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.Rules).NotEmpty().WithMessage("At least one rule is required.");
        RuleForEach(x => x.Rules).ChildRules(rule =>
        {
            rule.RuleFor(r => r.IndicatorType).NotEmpty();
            rule.RuleFor(r => r.Condition).NotEmpty();
            rule.RuleFor(r => r.SignalType).NotEmpty();
        });
    }
}
