using FluentValidation;
using TradingAssistant.Contracts.Commands;

namespace TradingAssistant.Application.Validators;

public class RunBacktestCommandValidator : AbstractValidator<RunBacktestCommand>
{
    public RunBacktestCommandValidator()
    {
        RuleFor(x => x.StrategyId).NotEmpty().WithMessage("StrategyId is required.");
        RuleFor(x => x.Symbol).NotEmpty().MaximumLength(10).WithMessage("Symbol is required.");
        RuleFor(x => x.StartDate).LessThan(x => x.EndDate).WithMessage("StartDate must be before EndDate.");
        RuleFor(x => x.EndDate).LessThanOrEqualTo(DateTime.UtcNow).WithMessage("EndDate cannot be in the future.");
    }
}
