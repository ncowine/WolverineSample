using FluentValidation;
using TradingAssistant.Contracts.Commands;

namespace TradingAssistant.Application.Validators;

public class PlaceOrderCommandValidator : AbstractValidator<PlaceOrderCommand>
{
    public PlaceOrderCommandValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty().WithMessage("AccountId is required.");
        RuleFor(x => x.Symbol).NotEmpty().MaximumLength(10).WithMessage("Symbol is required (max 10 chars).");
        RuleFor(x => x.Side).NotEmpty().Must(s => s == "Buy" || s == "Sell").WithMessage("Side must be 'Buy' or 'Sell'.");
        RuleFor(x => x.Type).NotEmpty().Must(t => t == "Market" || t == "Limit").WithMessage("Type must be 'Market' or 'Limit'.");
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Quantity must be greater than 0.");
        RuleFor(x => x.Price).GreaterThan(0).When(x => x.Type == "Limit").WithMessage("Price is required for limit orders.");
    }
}
