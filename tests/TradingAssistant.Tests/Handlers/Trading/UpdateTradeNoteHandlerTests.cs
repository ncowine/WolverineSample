using TradingAssistant.Application.Exceptions;
using TradingAssistant.Application.Handlers.Trading;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Domain.Trading;
using TradingAssistant.Tests.Helpers;

namespace TradingAssistant.Tests.Handlers.Trading;

public class UpdateTradeNoteHandlerTests
{
    private readonly FakeCurrentUser _user = new();

    private TradeNote SeedNote(Infrastructure.Persistence.TradingDbContext db,
        string content = "Original", string tags = "old-tag")
    {
        var note = new TradeNote
        {
            UserId = _user.UserId,
            Content = content,
            Tags = tags
        };
        db.TradeNotes.Add(note);
        db.SaveChanges();
        return note;
    }

    [Fact]
    public async Task Updates_content_and_tags()
    {
        using var db = TestDbContextFactory.Create();
        var note = SeedNote(db);
        var command = new UpdateTradeNoteCommand(note.Id, "Updated content",
            Tags: ["new-tag", "strategy"]);

        var result = await UpdateTradeNoteHandler.HandleAsync(command, db, _user);

        Assert.Equal("Updated content", result.Content);
        Assert.Equal(["new-tag", "strategy"], result.Tags);
        Assert.NotNull(result.UpdatedAt);
    }

    [Fact]
    public async Task Updates_content_without_changing_tags_when_tags_null()
    {
        using var db = TestDbContextFactory.Create();
        var note = SeedNote(db, tags: "keep-me");
        var command = new UpdateTradeNoteCommand(note.Id, "New content", Tags: null);

        var result = await UpdateTradeNoteHandler.HandleAsync(command, db, _user);

        Assert.Equal("New content", result.Content);
        Assert.Equal(["keep-me"], result.Tags);
    }

    [Fact]
    public async Task Clears_tags_when_empty_list_provided()
    {
        using var db = TestDbContextFactory.Create();
        var note = SeedNote(db, tags: "old");
        var command = new UpdateTradeNoteCommand(note.Id, "Content", Tags: []);

        var result = await UpdateTradeNoteHandler.HandleAsync(command, db, _user);

        Assert.Empty(result.Tags);
    }

    [Fact]
    public async Task Throws_when_note_not_found()
    {
        using var db = TestDbContextFactory.Create();
        var command = new UpdateTradeNoteCommand(Guid.NewGuid(), "Content");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => UpdateTradeNoteHandler.HandleAsync(command, db, _user));
    }

    [Fact]
    public async Task Throws_when_user_does_not_own_note()
    {
        using var db = TestDbContextFactory.Create();
        var note = new TradeNote
        {
            UserId = Guid.NewGuid(), // different user
            Content = "Not yours",
            Tags = ""
        };
        db.TradeNotes.Add(note);
        await db.SaveChangesAsync();

        var command = new UpdateTradeNoteCommand(note.Id, "Hacked");

        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => UpdateTradeNoteHandler.HandleAsync(command, db, _user));
    }
}
