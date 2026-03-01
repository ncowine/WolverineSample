namespace TradingAssistant.Application.Screening;

/// <summary>
/// Configurable weights for each confirmation check.
/// Default: all equal weight (1.0 each).
/// </summary>
public class ConfirmationWeights
{
    public decimal TrendAlignment { get; init; } = 1.0m;
    public decimal Momentum { get; init; } = 1.0m;
    public decimal Volume { get; init; } = 1.0m;
    public decimal Volatility { get; init; } = 1.0m;
    public decimal MacdHistogram { get; init; } = 1.0m;
    public decimal Stochastic { get; init; } = 1.0m;
}
