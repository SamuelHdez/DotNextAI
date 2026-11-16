namespace DotNextAI.Cli;

/// <summary>Process exit codes (docs/specs/feature-cli.md).</summary>
internal static class ExitCodes
{
    public const int Success = 0;

    /// <summary>The model call failed (proxy unreachable, key rejected, unknown model, timeout…).</summary>
    public const int ModelError = 1;

    /// <summary>Invalid input: unknown command or option, empty or too long question.</summary>
    public const int UsageError = 2;

    /// <summary>The LiteLLM proxy is not configured.</summary>
    public const int NotConfigured = 3;

    /// <summary>Cancelled with Ctrl+C (128 + SIGINT).</summary>
    public const int Cancelled = 130;
}
