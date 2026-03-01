using FluentValidation;
using TradingAssistant.Contracts.Commands;

namespace TradingAssistant.Application.Validators;

public class IngestMarketDataCommandValidator : AbstractValidator<IngestMarketDataCommand>
{
    public IngestMarketDataCommandValidator()
    {
        RuleFor(x => x.Symbol)
            .NotEmpty().WithMessage("Symbol is required.")
            .MaximumLength(10);

        RuleFor(x => x.YearsBack)
            .InclusiveBetween(1, 20)
            .WithMessage("YearsBack must be between 1 and 20.");
    }
}
