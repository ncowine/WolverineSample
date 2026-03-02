namespace TradingAssistant.Contracts.Queries;

public record GetMarketProfilesQuery;

public record GetMarketProfileQuery(string MarketCode);

public record GetCostProfilesQuery(string? MarketCode = null);

public record GetCurrentRegimeQuery(string MarketCode);

public record GetRegimeHistoryQuery(string MarketCode, int Page = 1, int PageSize = 20);

public record GetLatestBreadthQuery(string MarketCode);

public record GetCorrelationMatrixQuery;

public record GetPipelineStatusQuery(string? MarketCode = null);
