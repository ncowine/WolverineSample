namespace TradingAssistant.Contracts.Queries;

public record GetTradeChartDataQuery(Guid BacktestRunId, int TradeIndex, int BarsBefore = 20, int BarsAfter = 10);
