namespace TradingAssistant.Contracts.Queries;

public record GetBackfillStatusQuery(Guid JobId);

public record GetBackfillJobsQuery(Guid? UniverseId = null);
