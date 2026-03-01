namespace TradingAssistant.Contracts.Commands;

public record RemoveUniverseSymbolsCommand(Guid UniverseId, List<string> Symbols);
