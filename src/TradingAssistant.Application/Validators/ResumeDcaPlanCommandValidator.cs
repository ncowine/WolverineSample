using FluentValidation;
using TradingAssistant.Contracts.Commands;

namespace TradingAssistant.Application.Validators;

public class ResumeDcaPlanCommandValidator : AbstractValidator<ResumeDcaPlanCommand>
{
    public ResumeDcaPlanCommandValidator()
    {
        RuleFor(x => x.PlanId).NotEmpty().WithMessage("PlanId is required.");
    }
}
