namespace TradingAssistant.Contracts.Commands;

public record CreateStockUniverseCommand(string Name, string? Description = null, List<string>? Symbols = null);
