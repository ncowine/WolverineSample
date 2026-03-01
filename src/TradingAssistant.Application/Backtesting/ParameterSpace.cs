namespace TradingAssistant.Application.Backtesting;

/// <summary>
/// Defines a single parameter to optimize over: name, min, max, step.
/// </summary>
public class ParameterDefinition
{
    public string Name { get; init; } = string.Empty;
    public decimal Min { get; init; }
    public decimal Max { get; init; }
    public decimal Step { get; init; }

    /// <summary>
    /// Number of discrete values this parameter will take.
    /// </summary>
    public int ValueCount => Step > 0 ? (int)((Max - Min) / Step) + 1 : 1;

    /// <summary>
    /// Enumerate all values from Min to Max by Step.
    /// </summary>
    public IEnumerable<decimal> EnumerateValues()
    {
        for (var v = Min; v <= Max; v += Step)
            yield return v;
    }
}

/// <summary>
/// A collection of parameters to optimize. Generates the full Cartesian product.
/// </summary>
public class ParameterSpace
{
    public List<ParameterDefinition> Parameters { get; init; } = new();

    /// <summary>
    /// Total number of combinations (product of all parameter value counts).
    /// </summary>
    public long TotalCombinations => Parameters.Count == 0
        ? 0
        : Parameters.Aggregate(1L, (acc, p) => acc * p.ValueCount);

    /// <summary>
    /// True if total combinations exceeds 10,000.
    /// </summary>
    public bool IsLarge => TotalCombinations > 10_000;
}

/// <summary>
/// A single set of parameter values for one optimization trial.
/// </summary>
public class ParameterSet
{
    public Dictionary<string, decimal> Values { get; init; } = new();

    public decimal this[string name] => Values[name];

    public bool TryGet(string name, out decimal value) => Values.TryGetValue(name, out value);
}
