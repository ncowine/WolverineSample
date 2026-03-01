using TradingAssistant.SharedKernel;

namespace TradingAssistant.Domain.MarketData;

public class StockUniverse : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Comma-separated list of stock symbols (e.g. "AAPL,MSFT,GOOGL").
    /// </summary>
    public string Symbols { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Whether this universe includes SPY as a benchmark automatically.
    /// </summary>
    public bool IncludesBenchmark { get; set; } = true;

    public List<string> GetSymbolList() =>
        string.IsNullOrWhiteSpace(Symbols)
            ? new List<string>()
            : Symbols.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    public void SetSymbolList(IEnumerable<string> symbols) =>
        Symbols = string.Join(",", symbols.Select(s => s.Trim().ToUpperInvariant()).Distinct());
}
