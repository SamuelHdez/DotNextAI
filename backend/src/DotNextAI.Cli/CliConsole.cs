namespace DotNextAI.Cli;

/// <summary>Terminal streams used by the commands; tests replace them with in-memory readers and writers.</summary>
/// <param name="In">Standard input.</param>
/// <param name="Out">Standard output: only the answers, so they can be piped or redirected.</param>
/// <param name="Error">Standard error: prompts, warnings and errors.</param>
/// <param name="IsInputRedirected">True when stdin is a pipe or a file instead of a terminal.</param>
internal sealed record CliConsole(TextReader In, TextWriter Out, TextWriter Error, bool IsInputRedirected)
{
    public static CliConsole System() => new(Console.In, Console.Out, Console.Error, Console.IsInputRedirected);
}
