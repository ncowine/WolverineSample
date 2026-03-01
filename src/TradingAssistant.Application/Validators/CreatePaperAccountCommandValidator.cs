using FluentValidation;
using TradingAssistant.Contracts.Commands;

namespace TradingAssistant.Application.Validators;

public class CreatePaperAccountCommandValidator : AbstractValidator<CreatePaperAccountCommand>
{
    public CreatePaperAccountCommandValidator()
    {
        RuleFor(x => x.Name)
            .MaximumLength(200)
            .When(x => x.Name is not null)
            .WithMessage("Name must be 200 characters or fewer.");

        RuleFor(x => x.StartingBalance)
            .GreaterThan(0)
            .LessThanOrEqualTo(10_000_000m)
            .When(x => x.StartingBalance.HasValue)
            .WithMessage("Starting balance must be between 0 and 10,000,000.");
    }
}
