namespace TradingAssistant.Contracts.Queries;

public record GetFeatureSnapshotsQuery(
    string? Symbol = null,
    string? MarketCode = null,
    string? Outcome = null,
    int Page = 1,
    int PageSize = 50);
