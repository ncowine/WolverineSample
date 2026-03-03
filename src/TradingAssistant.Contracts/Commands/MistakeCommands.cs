namespace TradingAssistant.Contracts.Commands;

/// <summary>
/// Generate a mistake pattern report for a market.
/// Triggered every 50 trades or manually.
/// </summary>
public record GeneratePatternReportCommand(string MarketCode);
