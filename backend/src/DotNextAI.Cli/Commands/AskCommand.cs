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

        var images = new Option<string[]>("--image", "-i")
        {
            Description = $"Attach an image file (PNG, JPEG, WebP or GIF, up to {ChatRules.MaxImageBytes / 1024 / 1024} MB). Repeat for up to {ChatRules.MaxImagesPerQuestion} images.",
            Arity = ArgumentArity.ZeroOrMore,
            AllowMultipleArgumentsPerToken = false,
        };

        var command = new Command(Name, "Ask one question and print the answer.");
        command.Arguments.Add(question);
        command.Options.Add(images);
        command.SetAction((parseResult, cancellationToken) =>
            RunAsync(parseResult.GetValue(question), parseResult.GetValue(images), cancellationToken));
        return command;
    }

    public Task<int> RunAsync(string[]? words, CancellationToken cancellationToken) =>
        RunAsync(words, [], cancellationToken);

    public async Task<int> RunAsync(string[]? words, string[]? imagePaths, CancellationToken cancellationToken)
    {
        // Checked before asking for the question, so nobody types a question that cannot be sent.
        if (!chat.IsConfigured)
        {
            await console.Error.WriteLineAsync(CliMessages.NotConfigured);
            return ExitCodes.NotConfigured;
        }

        // Images are loaded before the question is read: a wrong path fails at once.
        var images = new List<ChatImage>();
        foreach (var path in imagePaths ?? [])
        {
            if (!ImageFiles.TryLoad(path, out var image, out var imageError))
            {
                await console.Error.WriteLineAsync(imageError);
                return ExitCodes.UsageError;
            }

            images.Add(image);
        }

        if (!ChatRules.TryValidateImages(images, out var imagesError))
        {
            await console.Error.WriteLineAsync(imagesError);
            return ExitCodes.UsageError;
        }

        try
        {
            var raw = await ReadQuestionAsync(words, cancellationToken);
            if (!ChatRules.TryNormalizeQuestion(raw, out var question, out var error))
            {
                await console.Error.WriteLineAsync(error);
                return ExitCodes.UsageError;
            }

            var answer = await chat.AskAsync(question, [], images, cancellationToken);
            await console.Out.WriteLineAsync(answer.Text);
            if (answer.Truncated)
            {
                await console.Error.WriteLineAsync(CliMessages.Truncated);
            }

            return ExitCodes.Success;
        }
        catch (ChatServiceException ex)
        {
            await CliMessages.WriteFailureAsync(console.Error, ex);
            return ex.Failure == ChatFailure.NotConfigured ? ExitCodes.NotConfigured : ExitCodes.ModelError;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await console.Error.WriteLineAsync(CliMessages.Cancelled);
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
}
