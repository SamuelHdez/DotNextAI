using System.CommandLine;
using DotNextAI.Application.Chat;

namespace DotNextAI.Cli.Commands;

/// <summary>
/// Backlog step 01: one question, one answer. The question comes from the arguments, from stdin when it is
/// redirected, or from an interactive prompt.
/// </summary>
internal sealed class AskCommand(IChatService chat, CliConsole console)
{
    public const string Name = "ask";

    public Command Build()
    {
        var question = new Argument<string[]>("question")
        {
            Description = "The question. Quotes are optional. Without it, the question is read from stdin or asked interactively.",
            Arity = ArgumentArity.ZeroOrMore,
        };

        var command = new Command(Name, "Ask one question and print the answer.");
        command.Arguments.Add(question);
        command.SetAction((parseResult, cancellationToken) =>
            RunAsync(parseResult.GetValue(question), cancellationToken));
        return command;
    }

    public async Task<int> RunAsync(string[]? words, CancellationToken cancellationToken)
    {
        // Checked before asking for the question, so nobody types a question that cannot be sent.
        if (!chat.IsConfigured)
        {
            await console.Error.WriteLineAsync(
                "The LiteLLM proxy is not configured. Run ./scripts/dev.sh secrets first.");
            return ExitCodes.NotConfigured;
        }

        try
        {
            var raw = await ReadQuestionAsync(words, cancellationToken);
            if (!ChatRules.TryNormalizeQuestion(raw, out var question, out var error))
            {
                await console.Error.WriteLineAsync(error);
                return ExitCodes.UsageError;
            }

            var answer = await chat.AskAsync(question, cancellationToken);
            await console.Out.WriteLineAsync(answer.Text);
            if (answer.Truncated)
            {
                await console.Error.WriteLineAsync("Warning: the answer was cut off because it reached the maximum length.");
            }

            return ExitCodes.Success;
        }
        catch (ChatServiceException ex)
        {
            await console.Error.WriteLineAsync($"Error: {ex.Message}");
            var hint = Hint(ex.Failure);
            if (hint is not null)
            {
                await console.Error.WriteLineAsync(hint);
            }

            return ex.Failure == ChatFailure.NotConfigured ? ExitCodes.NotConfigured : ExitCodes.ModelError;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await console.Error.WriteLineAsync("Cancelled.");
            return ExitCodes.Cancelled;
        }
    }

    private async Task<string?> ReadQuestionAsync(string[]? words, CancellationToken cancellationToken)
    {
        if (words is { Length: > 0 })
        {
            return string.Join(' ', words);
        }

        if (console.IsInputRedirected)
        {
            return await console.In.ReadToEndAsync(cancellationToken);
        }

        await console.Error.WriteAsync("Question: ");
        return await console.In.ReadLineAsync(cancellationToken);
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
