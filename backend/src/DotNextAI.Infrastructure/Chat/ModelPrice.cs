namespace DotNextAI.Infrastructure.Chat;

/// <summary>
/// Prices of a chat model in US dollars per token, as configured in the LiteLLM proxy (<c>model_info</c> of
/// <c>GET model/info</c>). The cost of a call follows LiteLLM's own rule, so it matches the spend the proxy tracks
/// (docs/specs/feature-token-metrics.md#decisions).
/// </summary>
/// <param name="InputPerToken">Price of an input token (<c>input_cost_per_token</c>).</param>
/// <param name="OutputPerToken">Price of an output token (<c>output_cost_per_token</c>).</param>
/// <param name="CachedInputPerToken">Price of a cached input token (<c>cache_read_input_token_cost</c>); null: the input price.</param>
/// <param name="Tiers">Prices that apply to the whole call when its input is above a threshold, e.g. 272k tokens.</param>
internal sealed record ModelPrice(
    decimal InputPerToken,
    decimal OutputPerToken,
    decimal? CachedInputPerToken = null,
    IReadOnlyList<ModelPriceTier>? Tiers = null)
{
    /// <summary>Decimals kept in a call's cost: enough for a fraction of a token, without the noise of the JSON doubles.</summary>
    public const int CostDecimals = 10;

    /// <summary>What one call cost: uncached input, cached input and output, each at its price.</summary>
    /// <param name="inputTokens">Every input token of the call, cached ones included.</param>
    /// <param name="cachedInputTokens">Input tokens read from the provider's cache (0 when not reported).</param>
    /// <param name="outputTokens">Output tokens, reasoning included.</param>
    public decimal CostOf(long inputTokens, long cachedInputTokens, long outputTokens)
    {
        var (input, output, cached) = (InputPerToken, OutputPerToken, CachedInputPerToken);

        // As LiteLLM does: the highest threshold the input is above sets every price of the call.
        if (Tiers?.OrderByDescending(tier => tier.InputTokensAbove).FirstOrDefault(tier => inputTokens > tier.InputTokensAbove) is { } tier)
        {
            (input, output, cached) = (tier.InputPerToken, tier.OutputPerToken ?? output, tier.CachedInputPerToken ?? cached);
        }

        var cachedTokens = Math.Clamp(cachedInputTokens, 0, Math.Max(inputTokens, 0));
        var cost = ((inputTokens - cachedTokens) * input) + (cachedTokens * (cached ?? input)) + (outputTokens * output);
        return Math.Round(cost, CostDecimals, MidpointRounding.AwayFromZero);
    }
}

/// <summary>Prices of a model above an input size (<c>input_cost_per_token_above_272k_tokens</c> and its siblings).</summary>
/// <param name="InputTokensAbove">The tier applies when the call's input tokens are above this number.</param>
/// <param name="OutputPerToken">Null: the model's base output price.</param>
/// <param name="CachedInputPerToken">Null: the model's base cache-read price.</param>
internal sealed record ModelPriceTier(long InputTokensAbove, decimal InputPerToken, decimal? OutputPerToken = null, decimal? CachedInputPerToken = null);
