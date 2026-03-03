namespace TradingAssistant.Contracts.Commands;

public record RetrainModelCommand(
    string MarketCode,
    int? MinFeatureVersion = null,
    int MaxSamples = 10_000,
    double TrainSplitRatio = 0.8);
