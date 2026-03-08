namespace TradingAssistant.Contracts.DTOs;

public record UserSettingsDto(
    string DefaultCurrency,
    decimal DefaultInitialCapital,
    string CostProfileMarket,
    string? BrokerSettingsJson);
