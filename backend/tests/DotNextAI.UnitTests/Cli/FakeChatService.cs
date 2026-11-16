using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using DotNextAI.Application.Chat;

namespace DotNextAI.UnitTests.Cli;

/// <summary>Chat service double: records each call (question and a copy of its history) and answers with a canned result.</summary>
internal sealed class FakeChatService(Func<string, ChatAnswer>? answer = null) : IChatService
{
    public bool IsConfigured { get; init; } = true;

    public List<(string Question, ChatTurn[] History, ChatImage[] Images)> Calls { get; } = [];

    public List<string> Questions => Calls.Select(call => call.Question).ToList();

    public Task<ChatAnswer> AskAsync(
        string question,
        IReadOnlyList<ChatTurn> history,
        IReadOnlyList<ChatImage> images,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls.Add((question, history.ToArray(), images.ToArray()));
        return Task.FromResult((answer ?? (q => new ChatAnswer($"answer to {q}", "gpt-5.6-luna", Truncated: false)))(question));
    }

    /// <summary>When set, the streamed answer breaks with this failure after its first piece.</summary>
    public ChatServiceException? FailMidStream { get; init; }

    /// <summary>Streams the canned answer word by word (the call is recorded like <see cref="AskAsync"/>).</summary>
    public async IAsyncEnumerable<ChatStreamUpdate> AskStreamingAsync(
        string question,
        IReadOnlyList<ChatTurn> history,
        IReadOnlyList<ChatImage> images,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var answer = await AskAsync(question, history, images, cancellationToken);
        var pieces = Regex.Split(answer.Text, @"(?<=\s)").Where(piece => piece.Length > 0).ToArray();
        for (var i = 0; i < pieces.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new ChatStreamUpdate(pieces[i]);
            if (i == 0 && FailMidStream is { } failure)
            {
                throw failure;
            }

            await Task.Yield();
        }

        yield return new ChatStreamUpdate(string.Empty, answer);
    }

    public static FakeChatService Failing(ChatFailure failure, string message) =>
        new(_ => throw new ChatServiceException(failure, message));
}
