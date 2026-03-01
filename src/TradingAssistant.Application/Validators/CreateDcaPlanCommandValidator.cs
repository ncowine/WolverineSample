using FluentValidation;
using TradingAssistant.Contracts.Commands;

namespace TradingAssistant.Application.Validators;

public class CreateDcaPlanCommandValidator : AbstractValidator<CreateDcaPlanCommand>
{
    public CreateDcaPlanCommandValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty().WithMessage("AccountId is required.");
        RuleFor(x => x.Symbol).NotEmpty().MaximumLength(10).WithMessage("Symbol is required (max 10 chars).");
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Amount must be greater than 0.");
        RuleFor(x => x.Frequency).NotEmpty()
            .Must(f => f is "Daily" or "Weekly" or "Biweekly" or "Monthly")
            .WithMessage("Frequency must be 'Daily', 'Weekly', 'Biweekly', or 'Monthly'.");
    }
}
