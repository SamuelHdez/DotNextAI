using System.CommandLine;
using DotNextAI.Application.Chat;

namespace DotNextAI.Cli.Commands;

/// <summary>Backlog step 08: lists the chat models of the proxy, the default marked with *.</summary>
internal sealed class ModelsCommand(IChatService chat, IModelCatalog catalog, CliConsole console)
{
    public const string Name = "models";

    public Command Build()
    {
        var command = new Command(Name, "List the chat models of the LiteLLM proxy (use one with --model or /model).");
        command.SetAction((_, cancellationToken) => RunAsync(cancellationToken));
        return command;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        if (!chat.IsConfigured)
        {
            await console.Error.WriteLineAsync(CliMessages.NotConfigured);
            return ExitCodes.NotConfigured;
        }

        try
        {
            var models = await catalog.GetChatModelsAsync(cancellationToken);
            foreach (var line in ModelChoice.Describe(models))
            {
                await console.Out.WriteLineAsync(line);
            }

            return ExitCodes.Success;
        }
        catch (ChatServiceException ex)
        {
            await CliMessages.WriteFailureAsync(console.Error, ex);
            return ExitCodes.ModelError;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await console.Error.WriteLineAsync(CliMessages.Cancelled);
            return ExitCodes.Cancelled;
        }
    }
}
