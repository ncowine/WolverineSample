namespace TradingAssistant.Contracts.Commands;

public record PromoteStrategyCommand(Guid EntryId, bool Force = false);
