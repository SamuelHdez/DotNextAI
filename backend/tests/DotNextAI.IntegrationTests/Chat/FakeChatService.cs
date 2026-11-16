using DotNextAI.Application.Chat;

namespace DotNextAI.IntegrationTests.Chat;

/// <summary>Chat service double: records each call and answers with a canned result or exception.</summary>
internal sealed class FakeChatService(Func<string, ChatAnswer>? answer = null) : IChatService
{
    public bool IsConfigured { get; init; } = true;

    public List<(string Question, ChatTurn[] History, ChatImage[] Images)> Calls { get; } = [];

    public Task<ChatAnswer> AskAsync(
        string question,
        IReadOnlyList<ChatTurn> history,
        IReadOnlyList<ChatImage> images,
        CancellationToken cancellationToken)
    {
        Calls.Add((question, history.ToArray(), images.ToArray()));
        return Task.FromResult((answer ?? (q => new ChatAnswer($"answer to {q}", "gpt-5.6-luna", Truncated: false)))(question));
    }

    public static FakeChatService Failing(ChatFailure failure, string message) =>
        new(_ => throw new ChatServiceException(failure, message));
}
