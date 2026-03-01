namespace TradingAssistant.Contracts.DTOs;

public record IngestMarketDataResponse(
    string Symbol,
    int DailyCandlesStored,
    int WeeklyCandlesStored,
    int MonthlyCandlesStored,
    DateTime? EarliestDate,
    DateTime? LatestDate,
    string Message);
