namespace TradingAssistant.Contracts.Commands;

/// <summary>
/// Trigger regime classification for a specific market.
/// If Date is null, uses current UTC date.
/// </summary>
public record ClassifyRegimeCommand(string MarketCode, DateTime? Date = null);
