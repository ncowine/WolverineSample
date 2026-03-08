namespace TradingAssistant.Contracts.Commands;

public record UpdateUserSettingsCommand(
    string DefaultCurrency,
    decimal DefaultInitialCapital,
    string CostProfileMarket,
    string? BrokerSettingsJson = null);
