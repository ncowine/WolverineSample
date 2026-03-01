namespace TradingAssistant.Contracts.Queries;

/// <summary>
/// Get latest screener results, optionally filtered by grade and date.
/// </summary>
public record GetScreenerResultsQuery(string? MinGrade = null, DateTime? Date = null);

/// <summary>
/// Get detailed signal for a specific symbol from the latest scan.
/// </summary>
public record GetScreenerSignalQuery(string Symbol);

/// <summary>
/// Get paged history of past screener scans.
/// </summary>
public record GetScreenerHistoryQuery(int Page = 1, int PageSize = 20);
