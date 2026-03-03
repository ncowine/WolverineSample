namespace TradingAssistant.Contracts.Commands;

public record EnterTournamentCommand(
    Guid TournamentRunId,
    Guid StrategyId,
    decimal PaperAccountBalance = 100_000m);
