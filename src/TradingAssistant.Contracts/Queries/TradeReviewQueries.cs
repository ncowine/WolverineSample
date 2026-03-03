namespace TradingAssistant.Contracts.Queries;

public record GetTradeReviewsQuery(
    string? Symbol = null,
    string? MarketCode = null,
    string? OutcomeClass = null,
    int Page = 1,
    int PageSize = 20);

public record GetTradeReviewByTradeIdQuery(Guid TradeId);
