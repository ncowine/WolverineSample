using FluentValidation;
using TradingAssistant.Contracts.Commands;

namespace TradingAssistant.Application.Validators;

public class CreateStockUniverseCommandValidator : AbstractValidator<CreateStockUniverseCommand>
{
    public CreateStockUniverseCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100);
    }
}
