using System.IO.Compression;
using System.Text;

namespace TradingAssistant.Application.Backtesting;

/// <summary>
/// Gzip compress/decompress JSON strings for storage-efficient persistence
/// of large backtest artifacts (equity curves, trade logs).
/// </summary>
public static class JsonCompression
{
    /// <summary>
    /// Compress a JSON string to a Base64-encoded gzip byte array.
    /// Returns empty string for null/empty input.
    /// </summary>
    public static string Compress(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return string.Empty;

        var bytes = Encoding.UTF8.GetBytes(json);
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal))
        {
            gzip.Write(bytes, 0, bytes.Length);
        }

        return Convert.ToBase64String(output.ToArray());
    }

    /// <summary>
    /// Decompress a Base64-encoded gzip string back to JSON.
    /// Returns empty string for null/empty input.
    /// </summary>
    public static string Decompress(string? compressed)
    {
        if (string.IsNullOrEmpty(compressed))
            return string.Empty;

        var bytes = Convert.FromBase64String(compressed);
        using var input = new MemoryStream(bytes);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);

        return Encoding.UTF8.GetString(output.ToArray());
    }
}
