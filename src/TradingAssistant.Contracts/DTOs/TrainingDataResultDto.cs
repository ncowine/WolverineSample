namespace TradingAssistant.Contracts.DTOs;

public record TrainingDataResultDto(
    int TotalSnapshots,
    int ConvertedVectors,
    int WinCount,
    int LossCount,
    int SkippedPending,
    int FeatureVersion,
    string[] FeatureColumns);
