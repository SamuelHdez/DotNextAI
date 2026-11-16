using System.CommandLine;
using DotNextAI.Application.Chat;
using DotNextAI.Cli.Commands;

namespace DotNextAI.Cli;

/// <summary>
/// Command tree of the terminal app. It grows with the backlog (chat history, streaming, models, images, tokens)
/// and stays available next to the web app (docs/specs/feature-cli.md).
/// </summary>
internal static class CliApplication
{
    public static RootCommand Build(IChatService chat, CliConsole console)
    {
        var ask = new AskCommand(chat, console);
        var chatCommand = new ChatCommand(chat, console);

        var root = new RootCommand("DotNextAI: an LLM chatbot in the terminal, through ERNI's LiteLLM proxy.");
        root.Subcommands.Add(chatCommand.Build());
        root.Subcommands.Add(ask.Build());

        // Without a command: the chat (step 03). Until step 03 it asked one question, as `ask` does.
        root.SetAction((_, cancellationToken) => chatCommand.RunAsync(cancellationToken));
        return root;
    }

    public static async Task<int> RunAsync(
        string[] args,
        IChatService chat,
        CliConsole console,
        CancellationToken cancellationToken = default)
    {
        var parseResult = Build(chat, console).Parse(args);

        // Unknown commands or options: list the errors and exit with the usage error code (System.CommandLine's
        // default would print the whole help and exit with 1, the model-error code).
        if (parseResult.Errors.Count > 0)
        {
            foreach (var error in parseResult.Errors)
            {
                await console.Error.WriteLineAsync(error.Message);
            }

            await console.Error.WriteLineAsync("Run 'dotnextai --help' for usage.");
            return ExitCodes.UsageError;
        }

        return await parseResult.InvokeAsync(
            new InvocationConfiguration { Output = console.Out, Error = console.Error },
            cancellationToken);
    }
}
