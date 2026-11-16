using DotNextAI.Application.Chat;

namespace DotNextAI.UnitTests.Cli;

/// <summary>Chat service double: records each call (question and a copy of its history) and answers with a canned result.</summary>
internal sealed class FakeChatService(Func<string, ChatAnswer>? answer = null) : IChatService
{
    public bool IsConfigured { get; init; } = true;

    public List<(string Question, ChatTurn[] History)> Calls { get; } = [];

    public List<string> Questions => Calls.Select(call => call.Question).ToList();

    public Task<ChatAnswer> AskAsync(string question, IReadOnlyList<ChatTurn> history, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls.Add((question, history.ToArray()));
        return Task.FromResult((answer ?? (q => new ChatAnswer($"answer to {q}", "gpt-5.6-luna", Truncated: false)))(question));
    }

    public static FakeChatService Failing(ChatFailure failure, string message) =>
        new(_ => throw new ChatServiceException(failure, message));
}
