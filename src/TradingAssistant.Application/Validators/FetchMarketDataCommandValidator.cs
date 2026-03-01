using FluentValidation;
using TradingAssistant.Contracts.Commands;

namespace TradingAssistant.Application.Validators;

public class FetchMarketDataCommandValidator : AbstractValidator<FetchMarketDataCommand>
{
    public FetchMarketDataCommandValidator()
    {
        RuleFor(x => x.Symbol)
            .NotEmpty().WithMessage("Symbol is required.")
            .MaximumLength(10);

        RuleFor(x => x.From)
            .LessThan(x => x.To)
            .When(x => x.From.HasValue && x.To.HasValue)
            .WithMessage("From date must be before To date.");

        RuleFor(x => x.To)
            .LessThanOrEqualTo(DateTime.UtcNow.Date.AddDays(1))
            .When(x => x.To.HasValue)
            .WithMessage("To date cannot be in the future.");
    }
}
