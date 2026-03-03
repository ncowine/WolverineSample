namespace TradingAssistant.Contracts.Commands;

public record ReviewTradeCommand(
    Guid TradeId,
    string Symbol,
    string MarketCode,
    string StrategyName,
    decimal EntryPrice,
    decimal ExitPrice,
    DateTime EntryDate,
    DateTime ExitDate,
    decimal PnlPercent,
    decimal PnlAbsolute,
    string RegimeAtEntry,
    string RegimeAtExit,
    decimal? Grade = null,
    float? MlConfidence = null);
