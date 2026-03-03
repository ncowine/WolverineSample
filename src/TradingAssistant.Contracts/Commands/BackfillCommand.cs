namespace TradingAssistant.Contracts.Commands;

public record BackfillCommand(Guid UniverseId, int YearsBack = 5, bool Incremental = false);
