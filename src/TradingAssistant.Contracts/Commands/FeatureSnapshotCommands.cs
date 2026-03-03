namespace TradingAssistant.Contracts.Commands;

public record CaptureFeatureSnapshotCommand(
    Guid OrderId,
    string Symbol,
    string MarketCode,
    decimal Price,
    string OrderSide);

public record UpdateFeatureOutcomeCommand(
    Guid OrderId,
    decimal PnlPercent);
