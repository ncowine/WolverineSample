using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using TradingAssistant.Infrastructure.MarketData;

namespace TradingAssistant.Tests.MarketData;

public class YahooFinanceProviderTests
{
    [Fact]
    public async Task Parses_yahoo_response_correctly()
    {
        var json = BuildYahooResponse(
            timestamps: [1704067200, 1704153600], // 2024-01-01, 2024-01-02
            opens: [100.0, 102.0],
            highs: [105.0, 107.0],
            lows: [99.0, 101.0],
            closes: [103.0, 106.0],
            adjCloses: [51.5, 53.0],
            volumes: [1000000, 2000000]);

        var handler = new FakeHttpMessageHandler(json, HttpStatusCode.OK);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.com") };
        var provider = new YahooFinanceProvider(httpClient, new NullLogger<YahooFinanceProvider>());

        var candles = await provider.GetDailyCandlesAsync("AAPL",
            new DateTime(2024, 1, 1), new DateTime(2024, 1, 2));

        Assert.Equal(2, candles.Count);

        Assert.Equal(new DateTime(2024, 1, 1), candles[0].Date);
        Assert.Equal(100.0m, candles[0].Open);
        Assert.Equal(105.0m, candles[0].High);
        Assert.Equal(99.0m, candles[0].Low);
        Assert.Equal(103.0m, candles[0].Close);
        Assert.Equal(51.5m, candles[0].AdjustedClose);
        Assert.Equal(1000000, candles[0].Volume);

        Assert.Equal(new DateTime(2024, 1, 2), candles[1].Date);
    }

    [Fact]
    public async Task Skips_null_candles()
    {
        // Yahoo returns null for some holidays
        var json = """
        {
          "chart": {
            "result": [{
              "timestamp": [1704067200, 1704153600, 1704240000],
              "indicators": {
                "quote": [{
                  "open": [100.0, null, 102.0],
                  "high": [105.0, null, 107.0],
                  "low": [99.0, null, 101.0],
                  "close": [103.0, null, 106.0],
                  "volume": [1000000, null, 2000000]
                }],
                "adjclose": [{
                  "adjclose": [51.5, null, 53.0]
                }]
              }
            }],
            "error": null
          }
        }
        """;

        var handler = new FakeHttpMessageHandler(json, HttpStatusCode.OK);
        var httpClient = new HttpClient(handler);
        var provider = new YahooFinanceProvider(httpClient, new NullLogger<YahooFinanceProvider>());

        var candles = await provider.GetDailyCandlesAsync("AAPL",
            new DateTime(2024, 1, 1), new DateTime(2024, 1, 3));

        Assert.Equal(2, candles.Count); // Skipped the null one
    }

    [Fact]
    public async Task Throws_on_api_error_response()
    {
        var json = """
        {
          "chart": {
            "result": null,
            "error": {
              "code": "Not Found",
              "description": "No data found, symbol may be delisted"
            }
          }
        }
        """;

        var handler = new FakeHttpMessageHandler(json, HttpStatusCode.OK);
        var httpClient = new HttpClient(handler);
        var provider = new YahooFinanceProvider(httpClient, new NullLogger<YahooFinanceProvider>());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.GetDailyCandlesAsync("INVALID", DateTime.UtcNow.AddDays(-5), DateTime.UtcNow));

        Assert.Contains("No data found", ex.Message);
    }

    [Fact]
    public async Task Throws_on_client_error()
    {
        var handler = new FakeHttpMessageHandler("", HttpStatusCode.NotFound);
        var httpClient = new HttpClient(handler);
        var provider = new YahooFinanceProvider(httpClient, new NullLogger<YahooFinanceProvider>());

        await Assert.ThrowsAsync<HttpRequestException>(
            () => provider.GetDailyCandlesAsync("AAPL", DateTime.UtcNow.AddDays(-5), DateTime.UtcNow));
    }

    [Fact]
    public async Task Validates_from_before_to()
    {
        var handler = new FakeHttpMessageHandler("", HttpStatusCode.OK);
        var httpClient = new HttpClient(handler);
        var provider = new YahooFinanceProvider(httpClient, new NullLogger<YahooFinanceProvider>());

        await Assert.ThrowsAsync<ArgumentException>(
            () => provider.GetDailyCandlesAsync("AAPL", DateTime.UtcNow, DateTime.UtcNow.AddDays(-5)));
    }

    [Fact]
    public async Task Validates_symbol_not_empty()
    {
        var handler = new FakeHttpMessageHandler("", HttpStatusCode.OK);
        var httpClient = new HttpClient(handler);
        var provider = new YahooFinanceProvider(httpClient, new NullLogger<YahooFinanceProvider>());

        await Assert.ThrowsAsync<ArgumentException>(
            () => provider.GetDailyCandlesAsync("", DateTime.UtcNow.AddDays(-5), DateTime.UtcNow));
    }

    [Fact]
    public async Task Retries_on_server_error()
    {
        // Fail twice with 500, then succeed
        var handler = new CountingHttpHandler(failCount: 2,
            successJson: BuildYahooResponse(
                timestamps: [1704067200],
                opens: [100.0], highs: [105.0], lows: [99.0],
                closes: [103.0], adjCloses: [103.0], volumes: [1000000]));

        var httpClient = new HttpClient(handler);
        var provider = new YahooFinanceProvider(httpClient, new NullLogger<YahooFinanceProvider>());

        var candles = await provider.GetDailyCandlesAsync("AAPL",
            new DateTime(2024, 1, 1), new DateTime(2024, 1, 1));

        Assert.Single(candles);
        Assert.Equal(3, handler.RequestCount); // 2 failures + 1 success
    }

    private static string BuildYahooResponse(
        long[] timestamps, double[] opens, double[] highs, double[] lows,
        double[] closes, double[] adjCloses, long[] volumes)
    {
        var ts = string.Join(",", timestamps);
        var o = string.Join(",", opens);
        var h = string.Join(",", highs);
        var l = string.Join(",", lows);
        var c = string.Join(",", closes);
        var ac = string.Join(",", adjCloses);
        var v = string.Join(",", volumes);

        return $$"""
        {
          "chart": {
            "result": [{
              "timestamp": [{{ts}}],
              "indicators": {
                "quote": [{
                  "open": [{{o}}],
                  "high": [{{h}}],
                  "low": [{{l}}],
                  "close": [{{c}}],
                  "volume": [{{v}}]
                }],
                "adjclose": [{
                  "adjclose": [{{ac}}]
                }]
              }
            }],
            "error": null
          }
        }
        """;
    }
}

internal class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly string _response;
    private readonly HttpStatusCode _statusCode;

    public FakeHttpMessageHandler(string response, HttpStatusCode statusCode)
    {
        _response = response;
        _statusCode = statusCode;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_response)
        });
    }
}

internal class CountingHttpHandler : HttpMessageHandler
{
    private readonly int _failCount;
    private readonly string _successJson;
    public int RequestCount { get; private set; }

    public CountingHttpHandler(int failCount, string successJson)
    {
        _failCount = failCount;
        _successJson = successJson;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestCount++;
        if (RequestCount <= _failCount)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(_successJson)
        });
    }
}
