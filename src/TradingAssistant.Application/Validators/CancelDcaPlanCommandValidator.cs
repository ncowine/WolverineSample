using FluentValidation;
using TradingAssistant.Contracts.Commands;

namespace TradingAssistant.Application.Validators;

public class CancelDcaPlanCommandValidator : AbstractValidator<CancelDcaPlanCommand>
{
    public CancelDcaPlanCommandValidator()
    {
        RuleFor(x => x.PlanId).NotEmpty().WithMessage("PlanId is required.");
    }
}
