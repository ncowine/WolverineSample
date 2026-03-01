using FluentValidation;
using TradingAssistant.Contracts.Commands;

namespace TradingAssistant.Application.Validators;

public class PauseDcaPlanCommandValidator : AbstractValidator<PauseDcaPlanCommand>
{
    public PauseDcaPlanCommandValidator()
    {
        RuleFor(x => x.PlanId).NotEmpty().WithMessage("PlanId is required.");
    }
}
