namespace TradingAssistant.Contracts.Commands;

public record CreateTournamentCommand(
    string MarketCode,
    string Description = "",
    int MaxEntries = 20);
