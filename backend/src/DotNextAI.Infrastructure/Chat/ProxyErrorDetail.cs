using System.ClientModel;
using System.Text.Json;

namespace DotNextAI.Infrastructure.Chat;

/// <summary>
/// Extracts a short, user-facing reason from an OpenAI-style error body (<c>{"error":{"message":"…"}}</c>) returned by
/// the LiteLLM proxy: first line only, at most <see cref="MaxLength"/> characters, with the API key masked if it ever
/// appears.
/// </summary>
internal static class ProxyErrorDetail
{
    public const int MaxLength = 300;

    public static string? From(ClientResultException exception, string? apiKey)
    {
        string? message;
        try
        {
            var body = exception.GetRawResponse()?.Content?.ToString();
            if (string.IsNullOrWhiteSpace(body))
            {
                return null;
            }

            using var json = JsonDocument.Parse(body);
            message = json.RootElement.TryGetProperty("error", out var error)
                && error.ValueKind == JsonValueKind.Object
                && error.TryGetProperty("message", out var text)
                && text.ValueKind == JsonValueKind.String
                    ? text.GetString()
                    : null;
        }
        catch (JsonException)
        {
            // Not an OpenAI-style body (e.g. an HTML error page of a gateway): no detail.
            return null;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var firstLine = message.Split('\n', 2)[0].Trim();
        if (!string.IsNullOrEmpty(apiKey))
        {
            firstLine = firstLine.Replace(apiKey, "***", StringComparison.Ordinal);
        }

        return firstLine.Length <= MaxLength ? firstLine : string.Concat(firstLine.AsSpan(0, MaxLength), "…");
    }
}
