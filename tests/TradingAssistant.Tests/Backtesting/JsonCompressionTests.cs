using System.Text.Json;
using TradingAssistant.Application.Backtesting;

namespace TradingAssistant.Tests.Backtesting;

public class JsonCompressionTests
{
    [Fact]
    public void Compress_and_decompress_roundtrip()
    {
        var original = JsonSerializer.Serialize(new { Name = "test", Value = 42 });

        var compressed = JsonCompression.Compress(original);
        var decompressed = JsonCompression.Decompress(compressed);

        Assert.Equal(original, decompressed);
    }

    [Fact]
    public void Compress_null_returns_empty()
    {
        Assert.Equal(string.Empty, JsonCompression.Compress(null));
    }

    [Fact]
    public void Compress_empty_returns_empty()
    {
        Assert.Equal(string.Empty, JsonCompression.Compress(string.Empty));
    }

    [Fact]
    public void Decompress_null_returns_empty()
    {
        Assert.Equal(string.Empty, JsonCompression.Decompress(null));
    }

    [Fact]
    public void Decompress_empty_returns_empty()
    {
        Assert.Equal(string.Empty, JsonCompression.Decompress(string.Empty));
    }

    [Fact]
    public void Compressed_is_base64()
    {
        var json = JsonSerializer.Serialize(new { Foo = "bar" });
        var compressed = JsonCompression.Compress(json);

        // Base64 should be valid
        var bytes = Convert.FromBase64String(compressed);
        Assert.True(bytes.Length > 0);
    }

    [Fact]
    public void Large_json_compresses_significantly()
    {
        // Simulate a 1000-point equity curve
        var points = Enumerable.Range(0, 1000)
            .Select(i => new { Date = DateTime.Today.AddDays(i).ToString("yyyy-MM-dd"), Value = 100000m + i * 10m })
            .ToList();
        var json = JsonSerializer.Serialize(points);

        var compressed = JsonCompression.Compress(json);

        // Compressed Base64 should be much smaller than raw JSON
        Assert.True(compressed.Length < json.Length,
            $"Compressed ({compressed.Length}) should be smaller than original ({json.Length})");
    }

    [Fact]
    public void Roundtrip_preserves_unicode_and_special_chars()
    {
        var original = "{\"name\":\"test\\\"quoted\\\"\",\"emoji\":\"\\u2764\"}";

        var compressed = JsonCompression.Compress(original);
        var decompressed = JsonCompression.Decompress(compressed);

        Assert.Equal(original, decompressed);
    }
}
