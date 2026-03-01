namespace TradingAssistant.Contracts.Commands;

public record AddUniverseSymbolsCommand(Guid UniverseId, List<string> Symbols);
