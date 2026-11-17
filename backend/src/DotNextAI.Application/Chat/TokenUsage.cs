namespace DotNextAI.Application.Chat;

/// <summary>
/// Tokens consumed by one model call, as the proxy reports them (backlog step 10). Input tokens include the whole
/// history sent with the question, so they grow with every turn of a conversation.
/// </summary>
/// <param name="Cost">
/// What the call cost in US dollars at the proxy's prices (docs/specs/feature-token-metrics.md#decisions); null when
/// the model has no price or the call has no usage.
/// </param>
public sealed record TokenUsage(long InputTokens, long OutputTokens, long TotalTokens, decimal? Cost = null)
{
    /// <summary>Nothing consumed yet: the start of a running total, with a known cost of 0.</summary>
    public static TokenUsage Zero { get; } = new(0, 0, 0, 0m);

    /// <summary>
    /// Sum of two calls, e.g. a conversation's running total. The cost becomes unknown as soon as one side has none, so
    /// a partial sum never passes for the total.
    /// </summary>
    public TokenUsage Add(TokenUsage other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new(
            InputTokens + other.InputTokens,
            OutputTokens + other.OutputTokens,
            TotalTokens + other.TotalTokens,
            Cost is { } cost && other.Cost is { } otherCost ? cost + otherCost : null);
    }

    /// <summary>A usage from the provider's counters; null when none is reported. A missing total is input + output.</summary>
    public static TokenUsage? From(long? input, long? output, long? total) =>
        input is null && output is null && total is null
            ? null
            : new(input ?? 0, output ?? 0, total ?? ((input ?? 0) + (output ?? 0)));
}
