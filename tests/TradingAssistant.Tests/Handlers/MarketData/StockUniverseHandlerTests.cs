using TradingAssistant.Application.Handlers.MarketData;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Domain.MarketData;
using TradingAssistant.Tests.Helpers;

namespace TradingAssistant.Tests.Handlers.MarketData;

public class StockUniverseHandlerTests
{
    [Fact]
    public async Task Creates_universe_with_symbols()
    {
        using var db = TestMarketDataDbContextFactory.Create();
        var command = new CreateStockUniverseCommand(
            "Tech Giants",
            "Top tech stocks",
            new List<string> { "AAPL", "MSFT", "GOOGL" });

        var result = await CreateStockUniverseHandler.HandleAsync(command, db);

        Assert.Equal("Tech Giants", result.Name);
        Assert.Equal("Top tech stocks", result.Description);
        Assert.Equal(3, result.Symbols.Count);
        Assert.Contains("AAPL", result.Symbols);
        Assert.Contains("MSFT", result.Symbols);
        Assert.Contains("GOOGL", result.Symbols);
        Assert.True(result.IsActive);
        Assert.True(result.IncludesBenchmark);
    }

    [Fact]
    public async Task Creates_universe_without_symbols()
    {
        using var db = TestMarketDataDbContextFactory.Create();
        var command = new CreateStockUniverseCommand("Empty Universe");

        var result = await CreateStockUniverseHandler.HandleAsync(command, db);

        Assert.Equal("Empty Universe", result.Name);
        Assert.Empty(result.Symbols);
    }

    [Fact]
    public async Task Rejects_duplicate_name()
    {
        using var db = TestMarketDataDbContextFactory.Create();
        var command = new CreateStockUniverseCommand("My Universe");

        await CreateStockUniverseHandler.HandleAsync(command, db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateStockUniverseHandler.HandleAsync(command, db));
    }

    [Fact]
    public async Task Trims_name_and_description()
    {
        using var db = TestMarketDataDbContextFactory.Create();
        var command = new CreateStockUniverseCommand("  Padded Name  ", "  desc  ");

        var result = await CreateStockUniverseHandler.HandleAsync(command, db);

        Assert.Equal("Padded Name", result.Name);
        Assert.Equal("desc", result.Description);
    }

    [Fact]
    public async Task Lists_all_universes()
    {
        using var db = TestMarketDataDbContextFactory.Create();
        await CreateStockUniverseHandler.HandleAsync(
            new CreateStockUniverseCommand("B Universe"), db);
        await CreateStockUniverseHandler.HandleAsync(
            new CreateStockUniverseCommand("A Universe"), db);

        var result = await GetStockUniversesHandler.HandleAsync(
            new GetStockUniversesQuery(), db);

        Assert.Equal(2, result.Count);
        Assert.Equal("A Universe", result[0].Name); // sorted by name
        Assert.Equal("B Universe", result[1].Name);
    }

    [Fact]
    public async Task Adds_symbols_to_universe()
    {
        using var db = TestMarketDataDbContextFactory.Create();
        var created = await CreateStockUniverseHandler.HandleAsync(
            new CreateStockUniverseCommand("Test", Symbols: new List<string> { "AAPL" }), db);

        var result = await AddUniverseSymbolsHandler.HandleAsync(
            new AddUniverseSymbolsCommand(created.Id, new List<string> { "MSFT", "GOOGL" }), db);

        Assert.Equal(3, result.Symbols.Count);
        Assert.Contains("AAPL", result.Symbols);
        Assert.Contains("MSFT", result.Symbols);
        Assert.Contains("GOOGL", result.Symbols);
    }

    [Fact]
    public async Task Add_symbols_deduplicates()
    {
        using var db = TestMarketDataDbContextFactory.Create();
        var created = await CreateStockUniverseHandler.HandleAsync(
            new CreateStockUniverseCommand("Test", Symbols: new List<string> { "AAPL", "MSFT" }), db);

        var result = await AddUniverseSymbolsHandler.HandleAsync(
            new AddUniverseSymbolsCommand(created.Id, new List<string> { "msft", "GOOGL" }), db);

        Assert.Equal(3, result.Symbols.Count); // MSFT not duplicated
    }

    [Fact]
    public async Task Add_symbols_throws_for_missing_universe()
    {
        using var db = TestMarketDataDbContextFactory.Create();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => AddUniverseSymbolsHandler.HandleAsync(
                new AddUniverseSymbolsCommand(Guid.NewGuid(), new List<string> { "AAPL" }), db));
    }

    [Fact]
    public async Task Removes_symbols_from_universe()
    {
        using var db = TestMarketDataDbContextFactory.Create();
        var created = await CreateStockUniverseHandler.HandleAsync(
            new CreateStockUniverseCommand("Test",
                Symbols: new List<string> { "AAPL", "MSFT", "GOOGL" }), db);

        var result = await RemoveUniverseSymbolsHandler.HandleAsync(
            new RemoveUniverseSymbolsCommand(created.Id, new List<string> { "MSFT" }), db);

        Assert.Equal(2, result.Symbols.Count);
        Assert.DoesNotContain("MSFT", result.Symbols);
        Assert.Contains("AAPL", result.Symbols);
        Assert.Contains("GOOGL", result.Symbols);
    }

    [Fact]
    public async Task Remove_symbols_is_case_insensitive()
    {
        using var db = TestMarketDataDbContextFactory.Create();
        var created = await CreateStockUniverseHandler.HandleAsync(
            new CreateStockUniverseCommand("Test",
                Symbols: new List<string> { "AAPL", "MSFT" }), db);

        var result = await RemoveUniverseSymbolsHandler.HandleAsync(
            new RemoveUniverseSymbolsCommand(created.Id, new List<string> { "msft" }), db);

        Assert.Single(result.Symbols);
        Assert.Contains("AAPL", result.Symbols);
    }
}
