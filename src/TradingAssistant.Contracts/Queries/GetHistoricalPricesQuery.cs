namespace TradingAssistant.Contracts.Queries;

public record GetHistoricalPricesQuery(
    string Symbol,
    DateTime StartDate,
    DateTime EndDate,
    string Interval = "Daily");
