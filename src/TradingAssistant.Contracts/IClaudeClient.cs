namespace TradingAssistant.Contracts;

/// <summary>
/// Request to send to Claude API.
/// </summary>
public record ClaudeRequest(
    string SystemPrompt,
    string UserPrompt,
    decimal Temperature = 0.7m,
    int MaxTokens = 4096);

/// <summary>
/// Response from Claude API.
/// </summary>
public record ClaudeResponse(
    string Content,
    int InputTokens,
    int OutputTokens,
    bool Success,
    string? Error = null);

/// <summary>
/// Abstraction over Claude API for testability.
/// </summary>
public interface IClaudeClient
{
    /// <summary>
    /// Send a completion request to Claude.
    /// </summary>
    Task<ClaudeResponse> CompleteAsync(ClaudeRequest request, CancellationToken ct = default);

    /// <summary>
    /// Remaining API calls available today (rate-limited to max daily count).
    /// </summary>
    int RemainingCallsToday { get; }

    /// <summary>
    /// Whether the daily rate limit has been reached.
    /// </summary>
    bool IsRateLimited { get; }
}
