namespace TradingAssistant.Contracts.Queries;

public record ListBacktestRunsQuery(Guid? StrategyId = null, int Page = 1, int PageSize = 20);
