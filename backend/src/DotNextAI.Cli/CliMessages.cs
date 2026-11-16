using DotNextAI.Application.Chat;

namespace DotNextAI.Cli;

/// <summary>Texts shared by the commands (docs/specs/feature-cli.md#messages).</summary>
internal static class CliMessages
{
    public const string NotConfigured = "The LiteLLM proxy is not configured. Run ./scripts/dev.sh secrets first.";

    public const string Truncated = "Warning: the answer was cut off because it reached the maximum length.";

    public const string Cancelled = "Cancelled.";

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
