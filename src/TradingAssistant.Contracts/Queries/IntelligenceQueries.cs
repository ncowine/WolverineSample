namespace TradingAssistant.Contracts.Queries;

public record GetMarketProfilesQuery;

public record GetMarketProfileQuery(string MarketCode);

public record GetCostProfilesQuery(string? MarketCode = null);
