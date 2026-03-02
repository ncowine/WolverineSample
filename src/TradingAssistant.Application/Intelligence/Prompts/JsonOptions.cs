using System.Text.Json;

namespace TradingAssistant.Application.Intelligence.Prompts;

/// <summary>
/// Shared JSON serializer options for prompt response parsing.
/// </summary>
internal static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
