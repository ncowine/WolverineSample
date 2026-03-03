namespace TradingAssistant.Contracts.Commands;

public record RetireStrategyCommand(Guid EntryId, string? Reason = null, bool Force = false);
