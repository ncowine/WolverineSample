namespace TradingAssistant.Contracts.Commands;

public record ComputeEnsembleSignalsCommand(
    string MarketCode,
    DateTime? Date = null,
    int MinAgreement = 2,
    bool UseWeightedVoting = false);
