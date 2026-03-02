namespace TradingAssistant.Application.Intelligence;

/// <summary>
/// Result of a geographic risk budget check.
/// </summary>
public record GeoBudgetCheckResult(
    bool Allowed,
    string CandidateMarket,
    decimal CurrentAllocationPercent,
    decimal ProposedAllocationPercent,
    decimal MaxAllocationPercent,
    decimal CurrentMarketNotional,
    decimal ProposedNotional,
    decimal TotalEquity,
    string Detail);

/// <summary>
/// Pure static geographic risk budget checker.
///
/// Enforces maximum allocation per market/region to prevent
/// over-concentration in a single geographic market.
/// Allocation is computed on notional value (shares × current price).
/// </summary>
public static class GeographicRiskBudget
{
    public const decimal DefaultMaxAllocationPercent = 50m;

    /// <summary>
    /// Check if adding a new position would exceed the geographic allocation limit.
    /// </summary>
    /// <param name="candidateMarket">Market code for the candidate position (e.g. "US_SP500").</param>
    /// <param name="proposedNotional">Notional value of the proposed position (shares × price).</param>
    /// <param name="marketNotionals">Current notional exposure per market code.</param>
    /// <param name="totalEquity">Total portfolio equity.</param>
    /// <param name="maxAllocationPercent">Maximum allowed allocation per market (default 50%).</param>
    public static GeoBudgetCheckResult Check(
        string candidateMarket,
        decimal proposedNotional,
        IReadOnlyDictionary<string, decimal> marketNotionals,
        decimal totalEquity,
        decimal maxAllocationPercent = DefaultMaxAllocationPercent)
    {
        if (totalEquity <= 0)
        {
            return new GeoBudgetCheckResult(
                Allowed: true,
                CandidateMarket: candidateMarket,
                CurrentAllocationPercent: 0m,
                ProposedAllocationPercent: 0m,
                MaxAllocationPercent: maxAllocationPercent,
                CurrentMarketNotional: 0m,
                ProposedNotional: proposedNotional,
                TotalEquity: totalEquity,
                Detail: "PASS: no equity to allocate against");
        }

        var currentNotional = marketNotionals.GetValueOrDefault(candidateMarket, 0m);
        var currentPct = Math.Round(currentNotional / totalEquity * 100m, 2);
        var proposedPct = Math.Round((currentNotional + proposedNotional) / totalEquity * 100m, 2);

        if (proposedPct > maxAllocationPercent)
        {
            return new GeoBudgetCheckResult(
                Allowed: false,
                CandidateMarket: candidateMarket,
                CurrentAllocationPercent: currentPct,
                ProposedAllocationPercent: proposedPct,
                MaxAllocationPercent: maxAllocationPercent,
                CurrentMarketNotional: currentNotional,
                ProposedNotional: proposedNotional,
                TotalEquity: totalEquity,
                Detail: $"BLOCKED: {candidateMarket} allocation {proposedPct:F1}% would exceed {maxAllocationPercent}% limit (current={currentPct:F1}%, proposed notional={proposedNotional:F2})");
        }

        return new GeoBudgetCheckResult(
            Allowed: true,
            CandidateMarket: candidateMarket,
            CurrentAllocationPercent: currentPct,
            ProposedAllocationPercent: proposedPct,
            MaxAllocationPercent: maxAllocationPercent,
            CurrentMarketNotional: currentNotional,
            ProposedNotional: proposedNotional,
            TotalEquity: totalEquity,
            Detail: $"PASS: {candidateMarket} allocation {proposedPct:F1}% within {maxAllocationPercent}% limit");
    }

    /// <summary>
    /// Compute notional exposure per market from a list of positions.
    /// </summary>
    /// <param name="positions">List of (symbol, notional value) tuples for open positions.</param>
    /// <param name="symbolToMarket">Mapping from symbol to market code.</param>
    /// <returns>Dictionary of market code → total notional exposure.</returns>
    public static Dictionary<string, decimal> ComputeMarketNotionals(
        IReadOnlyList<(string Symbol, decimal Notional)> positions,
        IReadOnlyDictionary<string, string> symbolToMarket)
    {
        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (var (symbol, notional) in positions)
        {
            if (symbolToMarket.TryGetValue(symbol, out var market))
            {
                result[market] = result.GetValueOrDefault(market) + notional;
            }
            // Symbols without a market mapping are ignored (ungrouped)
        }

        return result;
    }
}
