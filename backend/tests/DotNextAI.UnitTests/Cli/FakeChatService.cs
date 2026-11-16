using DotNextAI.Application.Chat;

namespace DotNextAI.UnitTests.Cli;

/// <summary>Chat service double for the CLI tests: records questions and answers with a canned result.</summary>
internal sealed class FakeChatService(Func<string, ChatAnswer>? answer = null) : IChatService
{
    public bool IsConfigured { get; init; } = true;

    public List<string> Questions { get; } = [];

    public Task<ChatAnswer> AskAsync(string question, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Questions.Add(question);
        return Task.FromResult((answer ?? (_ => new ChatAnswer("pong", "gpt-5.6-luna", Truncated: false)))(question));
    }

    public static FakeChatService Failing(ChatFailure failure, string message) =>
        new(_ => throw new ChatServiceException(failure, message));
}
