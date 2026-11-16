using System.Text.Json;
using DotNextAI.Application.Chat;
using DotNextAI.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace DotNextAI.Infrastructure.Chat;

/// <summary>
/// The proxy's model list (<c>GET {endpoint}models</c>, OpenAI-compatible, with LiteLLM's <c>mode</c> and token limits),
/// filtered to chat models and cached for <see cref="CacheDuration"/> (backlog step 08). Failures are not cached.
/// </summary>
internal sealed class LiteLlmModelCatalog(
    IOptions<LlmOptions> options,
    IHttpClientFactory httpClientFactory,
    TimeProvider timeProvider) : IModelCatalog, IDisposable
{
    public static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private readonly LlmOptions _options = options.Value;
    private readonly SemaphoreSlim _refresh = new(1, 1);
    private IReadOnlyList<ChatModel>? _cached;
    private DateTimeOffset _cachedUntil;

    public string? DefaultModel => _options.IsConfigured ? _options.DefaultModel : null;

    public void Dispose() => _refresh.Dispose();

    public async Task<IReadOnlyList<ChatModel>> GetChatModelsAsync(CancellationToken cancellationToken)
    {
        if (!_options.IsConfigured)
        {
            throw ChatFailureMapper.NotConfigured();
        }

        if (_cached is { } fresh && timeProvider.GetUtcNow() < _cachedUntil)
        {
            return fresh;
        }

        await _refresh.WaitAsync(cancellationToken);
        try
        {
            if (_cached is { } refreshed && timeProvider.GetUtcNow() < _cachedUntil)
            {
                return refreshed;
            }

            var models = await LoadAsync(cancellationToken);
            _cached = models;
            _cachedUntil = timeProvider.GetUtcNow() + CacheDuration;
            return models;
        }
        finally
        {
            _refresh.Release();
        }
    }

    private async Task<IReadOnlyList<ChatModel>> LoadAsync(CancellationToken cancellationToken)
    {
        var defaultModel = _options.DefaultModel!;
        var body = await LiteLlmHttp.SendAsync(httpClientFactory, _options, HttpMethod.Get, "models", jsonBody: null, defaultModel, cancellationToken);

        List<ChatModel> models;
        try
        {
            models = Parse(body, defaultModel);
        }
        catch (JsonException ex)
        {
            throw new ChatServiceException(ChatFailure.Unavailable, "The LiteLLM proxy answered the model list with invalid JSON.", ex);
        }

        // The configured default is always offered, first, even if the proxy does not list it.
        if (!models.Any(model => model.IsDefault))
        {
            models.Insert(0, new ChatModel(defaultModel, IsDefault: true));
        }

        return models
            .OrderByDescending(model => model.IsDefault)
            .ThenBy(model => model.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private static List<ChatModel> Parse(string body, string defaultModel)
    {
        using var json = JsonDocument.Parse(body);
        var models = new List<ChatModel>();
        if (!json.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return models;
        }

        foreach (var entry in data.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object || !entry.TryGetProperty("id", out var idElement) || idElement.GetString() is not { Length: > 0 } id)
            {
                continue;
            }

            // LiteLLM says what each model is for; a plain OpenAI-compatible list has no mode and is assumed to be chat.
            var mode = entry.TryGetProperty("mode", out var modeElement) && modeElement.ValueKind == JsonValueKind.String
                ? modeElement.GetString()
                : null;
            if (mode is not null && !string.Equals(mode, "chat", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (models.Any(model => model.Id == id))
            {
                continue;
            }

            models.Add(new ChatModel(id, string.Equals(id, defaultModel, StringComparison.Ordinal), Int(entry, "max_input_tokens"), Int(entry, "max_output_tokens")));
        }

        return models;
    }

    private static int? Int(JsonElement entry, string name) =>
        entry.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : null;
}
