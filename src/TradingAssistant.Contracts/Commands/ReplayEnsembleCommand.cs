namespace TradingAssistant.Contracts.Commands;

public record ReplayEnsembleCommand(
    string MarketCode,
    DateTime StartDate,
    DateTime EndDate,
    int MinAgreement = 2,
    bool UseWeightedVoting = false);
