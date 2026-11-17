using DotNextAI.Application.Chat;

namespace DotNextAI.Cli;

/// <summary>Texts shared by the commands (docs/specs/feature-cli.md#messages).</summary>
internal static class CliMessages
{
    public const string NotConfigured = "The LiteLLM proxy is not configured. Run ./scripts/dev.sh secrets first.";

    public const string Truncated = "Warning: the answer was cut off because it reached the maximum length.";

    public const string Cancelled = "Cancelled.";

    /// <summary>"12 in · 34 out · 46 total" (step 10), with invariant thousands separators.</summary>
    public static string Tokens(TokenUsage usage) =>
        string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{usage.InputTokens:N0} in · {usage.OutputTokens:N0} out · {usage.TotalTokens:N0} total");

    /// <summary>
    /// "$0.00014", "$0.012", "$1.23": US dollars with two significant digits below $1 and two decimals from $1, the same
    /// rule as the web app's <c>formatCost</c> (docs/specs/feature-token-metrics.md#formats).
    /// </summary>
    public static string Cost(decimal cost)
    {
        var decimals = cost is > 0 and < 1
            ? Math.Clamp(1 - (int)Math.Floor(Math.Log10((double)cost)), 2, 10)
            : 2;
        return "$" + Math.Round(cost, decimals, MidpointRounding.AwayFromZero).ToString("F" + decimals.ToString(System.Globalization.CultureInfo.InvariantCulture), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>" · $0.00014", or nothing when the cost is unknown.</summary>
    private static string CostSuffix(decimal? cost) => cost is { } known ? $" · {Cost(known)}" : string.Empty;

    /// <summary>
    /// The token line after an answer, on stderr, with the cost when it is known; nothing when the proxy reported no
    /// usage, so scripts and logs stay quiet with providers that do not count.
    /// </summary>
    public static async Task WriteUsageAsync(TextWriter error, TokenUsage? usage, TokenUsage? conversation = null)
    {
        if (usage is null)
        {
            return;
        }

        var line = $"Tokens: {Tokens(usage)}{CostSuffix(usage.Cost)}";
        if (conversation is not null)
        {
            line += string.Create(System.Globalization.CultureInfo.InvariantCulture, $" (conversation: {conversation.TotalTokens:N0}{CostSuffix(conversation.Cost)})");
        }

        await error.WriteLineAsync(line);
    }

    /// <summary>Writes <c>Error: …</c> and, when there is one, a hint line to stderr.</summary>
    public static async Task WriteFailureAsync(TextWriter error, ChatServiceException exception)
    {
        await error.WriteLineAsync($"Error: {exception.Message}");
        var hint = Hint(exception.Failure);
        if (hint is not null)
        {
            await error.WriteLineAsync(hint);
        }
    }

    private static string? Hint(ChatFailure failure) => failure switch
    {
        ChatFailure.NotConfigured or ChatFailure.Unauthorized => "Check the settings with ./scripts/dev.sh secrets.",
        ChatFailure.ModelNotFound => "Check Llm:DefaultModel with ./scripts/dev.sh secrets (see the proxy's model list).",
        ChatFailure.ModelUnavailable => "Try again later, or pick another model with ./scripts/dev.sh secrets.",
        ChatFailure.Unavailable or ChatFailure.Timeout => "Check the network (VPN) and Llm:Endpoint, then try again.",
        _ => null,
    };
}
