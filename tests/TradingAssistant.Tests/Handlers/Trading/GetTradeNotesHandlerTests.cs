using TradingAssistant.Application.Handlers.Trading;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Domain.Trading;
using TradingAssistant.Infrastructure.Persistence;
using TradingAssistant.Tests.Helpers;

namespace TradingAssistant.Tests.Handlers.Trading;

public class GetTradeNotesHandlerTests
{
    private readonly FakeCurrentUser _user = new();

    private void SeedNotes(TradingDbContext db, params TradeNote[] notes)
    {
        db.TradeNotes.AddRange(notes);
        db.SaveChanges();
    }

    private TradeNote MakeNote(string content, string tags = "",
        Guid? orderId = null, DateTime? createdAt = null)
    {
        var note = new TradeNote
        {
            UserId = _user.UserId,
            Content = content,
            Tags = tags,
            OrderId = orderId
        };
        if (createdAt.HasValue)
            note.CreatedAt = createdAt.Value;
        return note;
    }

    [Fact]
    public async Task Returns_only_current_user_notes()
    {
        using var db = TestDbContextFactory.Create();
        var otherUserId = Guid.NewGuid();
        SeedNotes(db,
            MakeNote("Mine", "tag1"),
            new TradeNote { UserId = otherUserId, Content = "Not mine", Tags = "" });

        var result = await GetTradeNotesHandler.HandleAsync(
            new GetTradeNotesQuery(), db, _user);

        Assert.Single(result.Items);
        Assert.Equal("Mine", result.Items[0].Content);
    }

    [Fact]
    public async Task Filters_by_tag()
    {
        using var db = TestDbContextFactory.Create();
        SeedNotes(db,
            MakeNote("Strategy note", "strategy,bullish"),
            MakeNote("Risk note", "risk"),
            MakeNote("No tags", ""));

        var result = await GetTradeNotesHandler.HandleAsync(
            new GetTradeNotesQuery(Tag: "strategy"), db, _user);

        Assert.Single(result.Items);
        Assert.Equal("Strategy note", result.Items[0].Content);
    }

    [Fact]
    public async Task Filters_by_date_range()
    {
        using var db = TestDbContextFactory.Create();
        SeedNotes(db,
            MakeNote("Old note", createdAt: new DateTime(2026, 1, 1)),
            MakeNote("In range", createdAt: new DateTime(2026, 2, 15)),
            MakeNote("Future note", createdAt: new DateTime(2026, 3, 15)));

        var result = await GetTradeNotesHandler.HandleAsync(
            new GetTradeNotesQuery(
                StartDate: new DateTime(2026, 2, 1),
                EndDate: new DateTime(2026, 2, 28)),
            db, _user);

        Assert.Single(result.Items);
        Assert.Equal("In range", result.Items[0].Content);
    }

    [Fact]
    public async Task Filters_by_order_id()
    {
        using var db = TestDbContextFactory.Create();
        var orderId = Guid.NewGuid();
        SeedNotes(db,
            MakeNote("With order", orderId: orderId),
            MakeNote("No order"));

        var result = await GetTradeNotesHandler.HandleAsync(
            new GetTradeNotesQuery(OrderId: orderId), db, _user);

        Assert.Single(result.Items);
        Assert.Equal("With order", result.Items[0].Content);
    }

    [Fact]
    public async Task Returns_paged_results()
    {
        using var db = TestDbContextFactory.Create();
        for (var i = 0; i < 5; i++)
            SeedNotes(db, MakeNote($"Note {i}", createdAt: DateTime.UtcNow.AddMinutes(-i)));

        var page1 = await GetTradeNotesHandler.HandleAsync(
            new GetTradeNotesQuery(Page: 1, PageSize: 2), db, _user);

        var page2 = await GetTradeNotesHandler.HandleAsync(
            new GetTradeNotesQuery(Page: 2, PageSize: 2), db, _user);

        var page3 = await GetTradeNotesHandler.HandleAsync(
            new GetTradeNotesQuery(Page: 3, PageSize: 2), db, _user);

        Assert.Equal(5, page1.TotalCount);
        Assert.Equal(3, page1.TotalPages);
        Assert.True(page1.HasNextPage);
        Assert.False(page1.HasPreviousPage);
        Assert.Equal(2, page1.Items.Count);

        Assert.Equal(2, page2.Items.Count);
        Assert.True(page2.HasPreviousPage);
        Assert.True(page2.HasNextPage);

        Assert.Single(page3.Items);
        Assert.False(page3.HasNextPage);
    }

    [Fact]
    public async Task Returns_tags_as_list_in_dto()
    {
        using var db = TestDbContextFactory.Create();
        SeedNotes(db, MakeNote("Tagged", "alpha,beta,gamma"));

        var result = await GetTradeNotesHandler.HandleAsync(
            new GetTradeNotesQuery(), db, _user);

        Assert.Equal(["alpha", "beta", "gamma"], result.Items[0].Tags);
    }

    [Fact]
    public async Task Returns_empty_tags_list_when_no_tags()
    {
        using var db = TestDbContextFactory.Create();
        SeedNotes(db, MakeNote("No tags", ""));

        var result = await GetTradeNotesHandler.HandleAsync(
            new GetTradeNotesQuery(), db, _user);

        Assert.Empty(result.Items[0].Tags);
    }

    [Fact]
    public async Task Combines_tag_and_date_filters()
    {
        using var db = TestDbContextFactory.Create();
        SeedNotes(db,
            MakeNote("Match", "strategy", createdAt: new DateTime(2026, 2, 15)),
            MakeNote("Wrong tag", "risk", createdAt: new DateTime(2026, 2, 15)),
            MakeNote("Wrong date", "strategy", createdAt: new DateTime(2026, 1, 1)));

        var result = await GetTradeNotesHandler.HandleAsync(
            new GetTradeNotesQuery(
                Tag: "strategy",
                StartDate: new DateTime(2026, 2, 1),
                EndDate: new DateTime(2026, 2, 28)),
            db, _user);

        Assert.Single(result.Items);
        Assert.Equal("Match", result.Items[0].Content);
    }

    [Fact]
    public async Task Orders_by_created_at_descending()
    {
        using var db = TestDbContextFactory.Create();
        SeedNotes(db,
            MakeNote("Oldest", createdAt: new DateTime(2026, 1, 1)),
            MakeNote("Newest", createdAt: new DateTime(2026, 3, 1)),
            MakeNote("Middle", createdAt: new DateTime(2026, 2, 1)));

        var result = await GetTradeNotesHandler.HandleAsync(
            new GetTradeNotesQuery(), db, _user);

        Assert.Equal("Newest", result.Items[0].Content);
        Assert.Equal("Middle", result.Items[1].Content);
        Assert.Equal("Oldest", result.Items[2].Content);
    }
}
