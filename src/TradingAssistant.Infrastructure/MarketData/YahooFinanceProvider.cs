using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TradingAssistant.Contracts.MarketData;

namespace TradingAssistant.Infrastructure.MarketData;

public class YahooFinanceProvider : IMarketDataProvider
{
    private readonly HttpClient _http;
    private readonly ILogger<YahooFinanceProvider> _logger;
    private readonly SemaphoreSlim _rateLimiter = new(5, 5); // max 5 concurrent requests
    private static readonly TimeSpan RateLimitWindow = TimeSpan.FromMilliseconds(200); // ~5 req/sec

    private const int MaxRetries = 3;
    private static readonly TimeSpan InitialBackoff = TimeSpan.FromSeconds(1);

    public YahooFinanceProvider(HttpClient http, ILogger<YahooFinanceProvider> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<IReadOnlyList<MarketCandle>> GetDailyCandlesAsync(
        string symbol,
        DateTime from,
        DateTime to,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        if (from > to)
            throw new ArgumentException("'from' date must be before 'to' date.");

        var period1 = new DateTimeOffset(from).ToUnixTimeSeconds();
        var period2 = new DateTimeOffset(to.Date.AddDays(1)).ToUnixTimeSeconds(); // inclusive end

        var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(symbol.ToUpperInvariant())}"
                  + $"?period1={period1}&period2={period2}&interval=1d&includeAdjustedClose=true";

        _logger.LogInformation("Fetching Yahoo Finance data for {Symbol} from {From:yyyy-MM-dd} to {To:yyyy-MM-dd}",
            symbol, from, to);

        var json = await FetchWithRetryAsync(url, ct);
        return ParseResponse(json, symbol);
    }

    private async Task<string> FetchWithRetryAsync(string url, CancellationToken ct)
    {
        for (var attempt = 0; attempt <= MaxRetries; attempt++)
        {
            await _rateLimiter.WaitAsync(ct);
            try
            {
                var response = await _http.GetAsync(url, ct);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync(ct);
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests ||
                    (int)response.StatusCode >= 500)
                {
                    if (attempt == MaxRetries)
                    {
                        _logger.LogError(
                            "Yahoo Finance request failed after {MaxRetries} retries. Status: {StatusCode}",
                            MaxRetries, response.StatusCode);
                        response.EnsureSuccessStatusCode(); // throws
                    }

                    var backoff = InitialBackoff * Math.Pow(2, attempt);
                    _logger.LogWarning(
                        "Yahoo Finance returned {StatusCode}. Retrying in {Backoff}s (attempt {Attempt}/{MaxRetries})",
                        response.StatusCode, backoff.TotalSeconds, attempt + 1, MaxRetries);

                    await Task.Delay(backoff, ct);
                    continue;
                }

                // 4xx (not 429) — don't retry
                _logger.LogError("Yahoo Finance request failed with {StatusCode} for URL: {Url}",
                    response.StatusCode, url);
                response.EnsureSuccessStatusCode();
            }
            finally
            {
                // Release after a short delay to enforce rate limiting
                _ = Task.Delay(RateLimitWindow, CancellationToken.None)
                    .ContinueWith(_ => _rateLimiter.Release(), CancellationToken.None);
            }
        }

        throw new InvalidOperationException("Unreachable — retry loop exhausted.");
    }

    private IReadOnlyList<MarketCandle> ParseResponse(string json, string symbol)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var chart = root.GetProperty("chart");
        var error = chart.GetProperty("error");
        if (error.ValueKind != JsonValueKind.Null)
        {
            var errorDesc = error.TryGetProperty("description", out var desc)
                ? desc.GetString()
                : "Unknown error";
            throw new InvalidOperationException($"Yahoo Finance API error for {symbol}: {errorDesc}");
        }

        var result = chart.GetProperty("result")[0];
        var timestamps = result.GetProperty("timestamp");
        var quote = result.GetProperty("indicators").GetProperty("quote")[0];
        var adjClose = result.GetProperty("indicators").GetProperty("adjclose")[0].GetProperty("adjclose");

        var opens = quote.GetProperty("open");
        var highs = quote.GetProperty("high");
        var lows = quote.GetProperty("low");
        var closes = quote.GetProperty("close");
        var volumes = quote.GetProperty("volume");

        var candles = new List<MarketCandle>();

        for (var i = 0; i < timestamps.GetArrayLength(); i++)
        {
            // Yahoo sometimes returns null values for holidays/missing data
            if (opens[i].ValueKind == JsonValueKind.Null ||
                closes[i].ValueKind == JsonValueKind.Null)
                continue;

            var timestamp = DateTimeOffset.FromUnixTimeSeconds(timestamps[i].GetInt64()).UtcDateTime.Date;

            candles.Add(new MarketCandle(
                Date: timestamp,
                Open: GetDecimal(opens[i]),
                High: GetDecimal(highs[i]),
                Low: GetDecimal(lows[i]),
                Close: GetDecimal(closes[i]),
                AdjustedClose: GetDecimal(adjClose[i]),
                Volume: volumes[i].ValueKind == JsonValueKind.Null ? 0 : volumes[i].GetInt64()));
        }

        _logger.LogInformation("Parsed {Count} candles for {Symbol}", candles.Count, symbol);
        return candles;
    }

    private static decimal GetDecimal(JsonElement el)
    {
        // Yahoo returns floats — convert cleanly to decimal
        return el.ValueKind == JsonValueKind.Null
            ? 0m
            : Math.Round((decimal)el.GetDouble(), 4);
    }
}
