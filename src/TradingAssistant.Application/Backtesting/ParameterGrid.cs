namespace TradingAssistant.Application.Backtesting;

/// <summary>
/// Generates the Cartesian product of all parameter values.
/// </summary>
public static class ParameterGrid
{
    /// <summary>
    /// Enumerate all parameter combinations (Cartesian product).
    /// </summary>
    public static IEnumerable<ParameterSet> Enumerate(ParameterSpace space)
    {
        if (space.Parameters.Count == 0)
            yield break;

        var paramNames = space.Parameters.Select(p => p.Name).ToArray();
        var paramValues = space.Parameters.Select(p => p.EnumerateValues().ToArray()).ToArray();

        var indices = new int[paramNames.Length];

        while (true)
        {
            var dict = new Dictionary<string, decimal>(paramNames.Length);
            for (var i = 0; i < paramNames.Length; i++)
                dict[paramNames[i]] = paramValues[i][indices[i]];

            yield return new ParameterSet { Values = dict };

            // Increment indices (odometer style)
            var pos = paramNames.Length - 1;
            while (pos >= 0)
            {
                indices[pos]++;
                if (indices[pos] < paramValues[pos].Length)
                    break;
                indices[pos] = 0;
                pos--;
            }

            if (pos < 0)
                yield break;
        }
    }
}
