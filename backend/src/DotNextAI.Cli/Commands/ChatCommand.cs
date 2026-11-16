using System.CommandLine;
using DotNextAI.Application.Chat;

namespace DotNextAI.Cli.Commands;

/// <summary>
/// Backlog step 03: a multi-turn chat. Every line is a question sent with the previous turns of the conversation;
/// lines starting with <c>/</c> are chat commands. The history lives only in this process.
/// </summary>
internal sealed class ChatCommand(IChatService chat, CliConsole console)
{
    public const string Name = "chat";

    public const string Banner = "DotNextAI chat. Type /help for commands, /exit or Ctrl+D to quit.";

    public const string Help =
        """
        Commands:
          /new         Start a new conversation (forget the previous questions and answers)
          /help        Show this list
          /exit, /quit Leave the chat (also Ctrl+D, or Ctrl+Z then Enter on Windows)
        """;

    public Command Build()
    {
        var command = new Command(Name, "Chat with the model, keeping the conversation history (the default command).");
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

        // With redirected stdin (a script or a file), every line is a question and only the answers are written.
        var interactive = !console.IsInputRedirected;
        var conversation = new ChatConversation(chat);
        if (interactive)
        {
            await console.Error.WriteLineAsync(Banner);
        }

        try
        {
            while (true)
            {
                if (interactive)
                {
                    await console.Error.WriteAsync("You: ");
                }

                var line = await console.In.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    if (interactive)
                    {
                        await console.Error.WriteLineAsync();
                    }

                    return ExitCodes.Success;
                }

                var input = line.Trim();
                if (input.Length == 0)
                {
                    continue;
                }

                if (input.StartsWith('/'))
                {
                    if (await RunChatCommandAsync(input, conversation))
                    {
                        return ExitCodes.Success;
                    }

                    continue;
                }

                await AskAsync(conversation, input, interactive, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await console.Error.WriteLineAsync();
            await console.Error.WriteLineAsync(CliMessages.Cancelled);
            return ExitCodes.Cancelled;
        }
    }

    private async Task AskAsync(ChatConversation conversation, string input, bool interactive, CancellationToken cancellationToken)
    {
        if (conversation.IsFull)
        {
            await console.Error.WriteLineAsync(
                $"The conversation reached the limit of {ChatRules.MaxConversationQuestions} questions. Type /new to start a new one.");
            return;
        }

        if (!ChatRules.TryNormalizeQuestion(input, out var question, out var error))
        {
            await console.Error.WriteLineAsync(error);
            return;
        }

        try
        {
            var answer = await conversation.AskAsync(question, cancellationToken);
            await console.Out.WriteLineAsync(answer.Text);
            if (answer.Truncated)
            {
                await console.Error.WriteLineAsync(CliMessages.Truncated);
            }
        }
        catch (ChatServiceException ex)
        {
            // The turn is not recorded: the user can ask again without breaking the conversation.
            await CliMessages.WriteFailureAsync(console.Error, ex);
        }

        if (interactive)
        {
            await console.Error.WriteLineAsync();
        }
    }

    /// <returns>True when the chat must end.</returns>
    private async Task<bool> RunChatCommandAsync(string input, ChatConversation conversation)
    {
        switch (input.ToUpperInvariant())
        {
            case "/EXIT" or "/QUIT":
                return true;
            case "/NEW":
                conversation.Reset();
                await console.Error.WriteLineAsync("Started a new conversation.");
                return false;
            case "/HELP":
                await console.Error.WriteLineAsync(Help);
                return false;
            default:
                await console.Error.WriteLineAsync($"Unknown command '{input}'. Type /help for the list.");
                return false;
        }
    }
}
