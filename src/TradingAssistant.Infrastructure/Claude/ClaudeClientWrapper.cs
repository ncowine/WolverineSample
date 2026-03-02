using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TradingAssistant.Contracts;

namespace TradingAssistant.Infrastructure.Claude;

/// <summary>
/// Production Claude API client with retry, rate limiting, and error handling.
/// Reads API key from ANTHROPIC_API_KEY env var or "Claude:ApiKey" in config.
/// </summary>
public sealed class ClaudeClientWrapper : IClaudeClient
{
    public const int DefaultMaxDailyCalls = 50;
    public const string DefaultModel = "claude-sonnet-4-20250514";

    private static readonly int[] RetryDelaysMs = [1_000, 5_000, 15_000];

    private readonly AnthropicClient _client;
    private readonly ILogger<ClaudeClientWrapper>? _logger;
    private readonly string _model;
    private readonly int _maxDailyCalls;

    private int _dailyCallCount;
    private DateTime _currentDay;
    private readonly object _rateLimitLock = new();

    public ClaudeClientWrapper(IConfiguration configuration, ILogger<ClaudeClientWrapper>? logger = null)
    {
        _logger = logger;

        var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
                     ?? configuration["Claude:ApiKey"]
                     ?? throw new InvalidOperationException(
                         "Claude API key not found. Set ANTHROPIC_API_KEY env var or Claude:ApiKey in config.");

        _model = configuration["Claude:Model"] ?? DefaultModel;
        _maxDailyCalls = int.TryParse(configuration["Claude:MaxDailyCalls"], out var max) ? max : DefaultMaxDailyCalls;

        _client = new AnthropicClient(apiKey);
        _currentDay = DateTime.UtcNow.Date;
    }

    /// <summary>
    /// Internal constructor for unit testing rate limiting logic only.
    /// </summary>
    internal ClaudeClientWrapper(int maxDailyCalls)
    {
        _maxDailyCalls = maxDailyCalls;
        _client = null!; // won't be used in rate limit tests
        _model = DefaultModel;
        _currentDay = DateTime.UtcNow.Date;
    }

    public int RemainingCallsToday
    {
        get
        {
            lock (_rateLimitLock)
            {
                ResetIfNewDay();
                return Math.Max(0, _maxDailyCalls - _dailyCallCount);
            }
        }
    }

    public bool IsRateLimited
    {
        get
        {
            lock (_rateLimitLock)
            {
                ResetIfNewDay();
                return _dailyCallCount >= _maxDailyCalls;
            }
        }
    }

    public async Task<ClaudeResponse> CompleteAsync(ClaudeRequest request, CancellationToken ct = default)
    {
        if (!TryConsumeRateLimit())
        {
            return new ClaudeResponse(
                Content: string.Empty,
                InputTokens: 0,
                OutputTokens: 0,
                Success: false,
                Error: $"Daily rate limit of {_maxDailyCalls} calls exceeded. Resets at midnight UTC.");
        }

        Exception? lastException = null;

        for (var attempt = 0; attempt <= RetryDelaysMs.Length; attempt++)
        {
            try
            {
                ct.ThrowIfCancellationRequested();

                var parameters = new MessageParameters
                {
                    Model = _model,
                    MaxTokens = request.MaxTokens,
                    Temperature = request.Temperature,
                    System = [new SystemMessage(request.SystemPrompt)],
                    Messages = [new Message(RoleType.User, request.UserPrompt)]
                };

                var response = await _client.Messages.GetClaudeMessageAsync(parameters, ct);

                var content = (response.Content?.FirstOrDefault() as TextContent)?.Text ?? string.Empty;

                _logger?.LogInformation(
                    "Claude API call succeeded (attempt {Attempt}): {InputTokens} in, {OutputTokens} out",
                    attempt + 1, response.Usage?.InputTokens ?? 0, response.Usage?.OutputTokens ?? 0);

                return new ClaudeResponse(
                    Content: content,
                    InputTokens: response.Usage?.InputTokens ?? 0,
                    OutputTokens: response.Usage?.OutputTokens ?? 0,
                    Success: true);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger?.LogWarning(ex, "Claude API call failed (attempt {Attempt}/{MaxAttempts})",
                    attempt + 1, RetryDelaysMs.Length + 1);

                if (attempt < RetryDelaysMs.Length)
                {
                    await Task.Delay(RetryDelaysMs[attempt], ct);
                }
            }
        }

        return new ClaudeResponse(
            Content: string.Empty,
            InputTokens: 0,
            OutputTokens: 0,
            Success: false,
            Error: $"All {RetryDelaysMs.Length + 1} attempts failed: {lastException?.Message}");
    }

    private bool TryConsumeRateLimit()
    {
        lock (_rateLimitLock)
        {
            ResetIfNewDay();
            if (_dailyCallCount >= _maxDailyCalls)
                return false;
            _dailyCallCount++;
            return true;
        }
    }

    private void ResetIfNewDay()
    {
        var today = DateTime.UtcNow.Date;
        if (_currentDay != today)
        {
            _currentDay = today;
            _dailyCallCount = 0;
        }
    }

    /// <summary>
    /// Exposed for testing: manually set the daily call count.
    /// </summary>
    internal void SetDailyCallCount(int count)
    {
        lock (_rateLimitLock)
        {
            _dailyCallCount = count;
        }
    }
}
