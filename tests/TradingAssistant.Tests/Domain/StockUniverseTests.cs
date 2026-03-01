using TradingAssistant.Domain.MarketData;

namespace TradingAssistant.Tests.Domain;

public class StockUniverseTests
{
    [Fact]
    public void GetSymbolList_returns_parsed_symbols()
    {
        var universe = new StockUniverse { Symbols = "AAPL,MSFT,GOOGL" };

        var symbols = universe.GetSymbolList();

        Assert.Equal(3, symbols.Count);
        Assert.Equal("AAPL", symbols[0]);
        Assert.Equal("MSFT", symbols[1]);
        Assert.Equal("GOOGL", symbols[2]);
    }

    [Fact]
    public void GetSymbolList_returns_empty_for_blank()
    {
        var universe = new StockUniverse { Symbols = "" };
        Assert.Empty(universe.GetSymbolList());

        universe.Symbols = "   ";
        Assert.Empty(universe.GetSymbolList());
    }

    [Fact]
    public void SetSymbolList_uppercases_and_deduplicates()
    {
        var universe = new StockUniverse();
        universe.SetSymbolList(new[] { "aapl", "AAPL", "msft", " googl " });

        var symbols = universe.GetSymbolList();
        Assert.Equal(3, symbols.Count);
        Assert.Contains("AAPL", symbols);
        Assert.Contains("MSFT", symbols);
        Assert.Contains("GOOGL", symbols);
    }

    [Fact]
    public void SetSymbolList_trims_whitespace()
    {
        var universe = new StockUniverse();
        universe.SetSymbolList(new[] { "  AAPL  ", " MSFT " });

        Assert.DoesNotContain(" ", universe.Symbols);
    }
}
