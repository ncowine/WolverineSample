namespace TradingAssistant.Contracts.Commands;

public record CreatePaperAccountCommand(string? Name = null, decimal? StartingBalance = null);
