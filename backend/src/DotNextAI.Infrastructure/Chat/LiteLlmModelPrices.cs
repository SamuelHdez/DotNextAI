using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using DotNextAI.Application.Chat;
using DotNextAI.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNextAI.Infrastructure.Chat;

/// <summary>Prices of the proxy's models, used to put a cost on each call (docs/specs/feature-token-metrics.md).</summary>
internal interface IModelPrices
{
    /// <summary>The prices of a model (the proxy's <c>model_name</c>), or null when it has none or they cannot be read.</summary>
    /// <remarks>Never throws for a proxy failure: a missing price only means no cost is shown.</remarks>
    Task<ModelPrice?> FindAsync(string model, CancellationToken cancellationToken);
}

/// <summary>
/// The prices configured in the LiteLLM proxy: <c>GET {endpoint}model/info</c>, the <c>model_info</c> of every
/// <c>model_name</c> in US dollars per token. Cached for <see cref="CacheDuration"/>; after a failure no prices are
/// offered for <see cref="RetryAfterFailure"/>, so a proxy without the route does not slow down every answer.
/// </summary>
internal sealed partial class LiteLlmModelPrices(
    IOptions<LlmOptions> options,
    IHttpClientFactory httpClientFactory,
    TimeProvider timeProvider,
    ILogger<LiteLlmModelPrices> logger) : IModelPrices, IDisposable
{
    public static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public static readonly TimeSpan RetryAfterFailure = TimeSpan.FromMinutes(1);

    private static readonly IReadOnlyDictionary<string, ModelPrice> NoPrices = new Dictionary<string, ModelPrice>(StringComparer.Ordinal);

    private readonly LlmOptions _options = options.Value;
    private readonly SemaphoreSlim _refresh = new(1, 1);
    private IReadOnlyDictionary<string, ModelPrice>? _cached;
    private DateTimeOffset _cachedUntil;

    public void Dispose() => _refresh.Dispose();

    public async Task<ModelPrice?> FindAsync(string model, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        if (!_options.IsConfigured)
        {
            return null;
        }

        var prices = await GetPricesAsync(cancellationToken);
        return prices.GetValueOrDefault(model);
    }

    private async Task<IReadOnlyDictionary<string, ModelPrice>> GetPricesAsync(CancellationToken cancellationToken)
    {
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

            IReadOnlyDictionary<string, ModelPrice> prices;
            TimeSpan keep;
            try
            {
                var body = await LiteLlmHttp.SendAsync(httpClientFactory, _options, HttpMethod.Get, "model/info", jsonBody: null, _options.DefaultModel!, cancellationToken);
                (prices, keep) = (Parse(body), CacheDuration);
            }
            catch (ChatServiceException ex)
            {
                LogPricesUnavailable(ex.Failure);
                (prices, keep) = (NoPrices, RetryAfterFailure);
            }
            catch (JsonException)
            {
                LogPricesUnavailable(ChatFailure.Unavailable);
                (prices, keep) = (NoPrices, RetryAfterFailure);
            }

            _cached = prices;
            _cachedUntil = timeProvider.GetUtcNow() + keep;
            return prices;
        }
        finally
        {
            _refresh.Release();
        }
    }

    /// <summary>The priced models of a <c>model/info</c> answer; the first entry with prices wins for each model.</summary>
    internal static IReadOnlyDictionary<string, ModelPrice> Parse(string body)
    {
        using var json = JsonDocument.Parse(body);
        var prices = new Dictionary<string, ModelPrice>(StringComparer.Ordinal);
        if (!json.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return prices;
        }

        foreach (var entry in data.EnumerateArray())
        {
            if (entry.ValueKind == JsonValueKind.Object
                && entry.TryGetProperty("model_name", out var nameElement)
                && nameElement.GetString() is { Length: > 0 } name
                && !prices.ContainsKey(name)
                && entry.TryGetProperty("model_info", out var info)
                && info.ValueKind == JsonValueKind.Object
                && PriceOf(info) is { } price)
            {
                prices.Add(name, price);
            }
        }

        return prices;
    }

    private static ModelPrice? PriceOf(JsonElement info)
    {
        var input = Number(info, "input_cost_per_token");
        var output = Number(info, "output_cost_per_token");

        // LiteLLM reports 0 for a model nobody priced: that is an unknown price, not a free model.
        if ((input ?? 0) == 0 && (output ?? 0) == 0)
        {
            return null;
        }

        var tiers = new List<ModelPriceTier>();
        foreach (var property in info.EnumerateObject())
        {
            var match = TierKey().Match(property.Name);
            if (!match.Success || Number(info, property.Name) is not { } tierInput)
            {
                continue;
            }

            var threshold = long.Parse(match.Groups["count"].Value, CultureInfo.InvariantCulture) * (match.Groups["k"].Success ? 1000 : 1);
            var suffix = $"above_{match.Groups["count"].Value}{match.Groups["k"].Value}_tokens";
            tiers.Add(new ModelPriceTier(
                threshold,
                tierInput,
                Number(info, $"output_cost_per_token_{suffix}"),
                Number(info, $"cache_read_input_token_cost_{suffix}")));
        }

        return new ModelPrice(input ?? 0, output ?? 0, Number(info, "cache_read_input_token_cost"), tiers.Count == 0 ? null : tiers);
    }

    // Through double on purpose: (decimal)double keeps 15 significant digits, so 1.0000000000000001e-07 becomes 1E-07.
    private static decimal? Number(JsonElement info, string name) =>
        info.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) && number is >= 0 and < 1_000_000
            ? (decimal)number
            : null;

    // input_cost_per_token_above_272k_tokens; the _flex (service tier) variants end differently and are left out.
    [GeneratedRegex("^input_cost_per_token_above_(?<count>[0-9]{1,9})(?<k>k)?_tokens$", RegexOptions.CultureInvariant)]
    private static partial Regex TierKey();

    // Only the failure kind: never the proxy's answer, which could echo configuration.
    [LoggerMessage(Level = LogLevel.Warning, Message = "The model prices could not be read from the LiteLLM proxy ({Failure}); answers show no cost for now")]
    private partial void LogPricesUnavailable(ChatFailure failure);
}
