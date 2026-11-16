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
          /image <path> Attach an image (PNG, JPEG, WebP or GIF) to your next question; repeat for up to 4
          /images       List the images attached to your next question
          /new          Start a new conversation (forget the previous questions, answers and attached images)
          /help         Show this list
          /exit, /quit  Leave the chat (also Ctrl+D, or Ctrl+Z then Enter on Windows)
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
        var attachments = new Attachments();
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
                    if (await RunChatCommandAsync(input, conversation, attachments))
                    {
                        return ExitCodes.Success;
                    }

                    continue;
                }

                await AskAsync(conversation, input, attachments, interactive, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await console.Error.WriteLineAsync();
            await console.Error.WriteLineAsync(CliMessages.Cancelled);
            return ExitCodes.Cancelled;
        }
    }

    private async Task AskAsync(
        ChatConversation conversation,
        string input,
        Attachments attachments,
        bool interactive,
        CancellationToken cancellationToken)
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
            var answer = await AnswerWriter.WriteAsync(
                conversation.AskStreamingAsync(question, attachments.Images, cancellationToken),
                console.Out);
            // Images go with one question only (the history keeps the text); after an answer they are used up.
            attachments.Clear();
            if (answer.Truncated)
            {
                await console.Error.WriteLineAsync(CliMessages.Truncated);
            }
        }
        catch (ChatServiceException ex)
        {
            // The turn is not recorded and the images stay attached: the user can ask again as it was.
            await CliMessages.WriteFailureAsync(console.Error, ex);
        }

        if (interactive)
        {
            await console.Error.WriteLineAsync();
        }
    }

    /// <returns>True when the chat must end.</returns>
    private async Task<bool> RunChatCommandAsync(string input, ChatConversation conversation, Attachments attachments)
    {
        var separator = input.IndexOf(' ', StringComparison.Ordinal);
        var name = (separator < 0 ? input : input[..separator]).ToUpperInvariant();
        var argument = separator < 0 ? string.Empty : input[(separator + 1)..].Trim();

        switch (name)
        {
            case "/IMAGE":
                await AttachAsync(argument, attachments);
                return false;
            case "/IMAGES":
                await console.Error.WriteLineAsync(attachments.Count == 0
                    ? "No images attached."
                    : $"Attached to your next question: {string.Join(", ", attachments.Descriptions)}");
                return false;
            case "/EXIT" or "/QUIT":
                return true;
            case "/NEW":
                conversation.Reset();
                attachments.Clear();
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

    private async Task AttachAsync(string path, Attachments attachments)
    {
        if (path.Length == 0)
        {
            await console.Error.WriteLineAsync("Usage: /image <path to a PNG, JPEG, WebP or GIF file>");
            return;
        }

        if (attachments.Count >= ChatRules.MaxImagesPerQuestion)
        {
            await console.Error.WriteLineAsync($"You can attach up to {ChatRules.MaxImagesPerQuestion} images to one question.");
            return;
        }

        if (!ImageFiles.TryLoad(path, out var image, out var error))
        {
            await console.Error.WriteLineAsync(error);
            return;
        }

        attachments.Add(image, ImageFiles.Describe(image, path));
        await console.Error.WriteLineAsync($"Attached {attachments.Descriptions[^1]}. It goes with your next question.");
    }

    /// <summary>Images waiting for the next question.</summary>
    private sealed class Attachments
    {
        private readonly List<ChatImage> _images = [];
        private readonly List<string> _descriptions = [];

        public IReadOnlyList<ChatImage> Images => _images;

        public IReadOnlyList<string> Descriptions => _descriptions;

        public int Count => _images.Count;

        public void Add(ChatImage image, string description)
        {
            _images.Add(image);
            _descriptions.Add(description);
        }

        public void Clear()
        {
            _images.Clear();
            _descriptions.Clear();
        }
    }
}
