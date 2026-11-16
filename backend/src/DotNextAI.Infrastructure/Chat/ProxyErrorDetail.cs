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

    public static string? From(ClientResultException exception, string? apiKey) =>
        FromBody(exception.GetRawResponse()?.Content?.ToString(), apiKey);

    public static string? FromBody(string? body, string? apiKey)
    {
        string? message;
        try
        {
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

    /// <summary>
    /// The machine-readable <c>error.type</c> of the body (e.g. LiteLLM's <c>team_model_access_denied</c>), safe to use
    /// for 401/403 because it never contains the key; null when there is none.
    /// </summary>
    public static string? ErrorType(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var json = JsonDocument.Parse(body);
            return json.RootElement.TryGetProperty("error", out var error)
                && error.ValueKind == JsonValueKind.Object
                && error.TryGetProperty("type", out var type)
                && type.ValueKind == JsonValueKind.String
                    ? type.GetString()
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
