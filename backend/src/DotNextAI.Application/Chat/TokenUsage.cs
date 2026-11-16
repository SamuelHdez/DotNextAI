namespace DotNextAI.Application.Chat;

/// <summary>
/// Tokens consumed by one model call, as the proxy reports them (backlog step 10). Input tokens include the whole
/// history sent with the question, so they grow with every turn of a conversation.
/// </summary>
public sealed record TokenUsage(long InputTokens, long OutputTokens, long TotalTokens)
{
    public static TokenUsage Zero { get; } = new(0, 0, 0);

    /// <summary>Sum of two calls, e.g. a conversation's running total.</summary>
    public TokenUsage Add(TokenUsage other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new(InputTokens + other.InputTokens, OutputTokens + other.OutputTokens, TotalTokens + other.TotalTokens);
    }

    /// <summary>A usage from the provider's counters; null when none is reported. A missing total is input + output.</summary>
    public static TokenUsage? From(long? input, long? output, long? total) =>
        input is null && output is null && total is null
            ? null
            : new(input ?? 0, output ?? 0, total ?? ((input ?? 0) + (output ?? 0)));
}
