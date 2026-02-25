namespace TradingAssistant.Contracts.DTOs;

public record SeedMarketDataResponse(string Message, int StocksCreated, int CandlesCreated, Guid DefaultAccountId);
