namespace TradingAssistant.Contracts.DTOs;

public record EnsembleSignalDto(
    Guid Id,
    string MarketCode,
    string Symbol,
    DateTime SignalDate,
    string Direction,
    decimal Confidence,
    string VotingMode,
    int MinAgreement,
    int TotalVoters,
    int AgreeingVoters,
    IReadOnlyList<StrategyVoteDto> Votes);

public record StrategyVoteDto(
    Guid StrategyId,
    string StrategyName,
    string Signal,
    decimal SharpeRatio,
    decimal Weight);

public record EnsembleComputeResultDto(
    bool Success,
    string MarketCode,
    DateTime SignalDate,
    int TotalSymbolsEvaluated,
    int ConsensusSignals,
    IReadOnlyList<EnsembleSignalDto> Signals,
    string? Error = null);

public record EnsembleReplayResultDto(
    string MarketCode,
    DateTime StartDate,
    DateTime EndDate,
    int TotalDays,
    int TotalSignals,
    int BuySignals,
    int SellSignals,
    IReadOnlyList<EnsembleSignalDto> Signals);
