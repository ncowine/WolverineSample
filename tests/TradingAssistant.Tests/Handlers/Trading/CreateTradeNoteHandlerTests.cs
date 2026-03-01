using TradingAssistant.Application.Handlers.Trading;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Tests.Helpers;

namespace TradingAssistant.Tests.Handlers.Trading;

public class CreateTradeNoteHandlerTests
{
    private readonly FakeCurrentUser _user = new();

    [Fact]
    public async Task Creates_note_with_tags()
    {
        using var db = TestDbContextFactory.Create();
        var command = new CreateTradeNoteCommand(null, null, "Buy signal on AAPL",
            Tags: ["strategy", "bullish"]);

        var result = await CreateTradeNoteHandler.HandleAsync(command, db, _user);

        Assert.Equal("Buy signal on AAPL", result.Content);
        Assert.Equal(["strategy", "bullish"], result.Tags);
        Assert.Equal(_user.UserId, db.TradeNotes.Single().UserId);
    }

    [Fact]
    public async Task Creates_note_without_tags()
    {
        using var db = TestDbContextFactory.Create();
        var command = new CreateTradeNoteCommand(null, null, "Simple note");

        var result = await CreateTradeNoteHandler.HandleAsync(command, db, _user);

        Assert.Equal("Simple note", result.Content);
        Assert.Empty(result.Tags);
    }

    [Fact]
    public async Task Stores_tags_as_comma_separated_string()
    {
        using var db = TestDbContextFactory.Create();
        var command = new CreateTradeNoteCommand(null, null, "Test",
            Tags: ["alpha", "beta", "gamma"]);

        await CreateTradeNoteHandler.HandleAsync(command, db, _user);

        var entity = db.TradeNotes.Single();
        Assert.Equal("alpha,beta,gamma", entity.Tags);
    }

    [Fact]
    public async Task Trims_content_and_tags()
    {
        using var db = TestDbContextFactory.Create();
        var command = new CreateTradeNoteCommand(null, null, "  padded content  ",
            Tags: ["  spaced ", " tag "]);

        var result = await CreateTradeNoteHandler.HandleAsync(command, db, _user);

        Assert.Equal("padded content", result.Content);
        Assert.Equal(["spaced", "tag"], result.Tags);
    }

    [Fact]
    public async Task Filters_out_empty_tags()
    {
        using var db = TestDbContextFactory.Create();
        var command = new CreateTradeNoteCommand(null, null, "Test",
            Tags: ["valid", "", "  ", "also-valid"]);

        var result = await CreateTradeNoteHandler.HandleAsync(command, db, _user);

        Assert.Equal(["valid", "also-valid"], result.Tags);
    }
}
