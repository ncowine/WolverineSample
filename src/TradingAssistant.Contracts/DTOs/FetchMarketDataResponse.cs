namespace TradingAssistant.Contracts.DTOs;

public record FetchMarketDataResponse(
    string Symbol,
    int CandlesFetched,
    int CandlesStored,
    DateTime? EarliestDate,
    DateTime? LatestDate,
    string Message);
