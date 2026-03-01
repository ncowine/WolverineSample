namespace TradingAssistant.Application.Indicators;

/// <summary>
/// Common interface for single-series indicator calculators.
/// Returns a decimal[] aligned to the input array — warmup values are 0.
/// </summary>
public interface IIndicatorCalculator
{
    decimal[] Calculate(decimal[] prices, int period);
}
