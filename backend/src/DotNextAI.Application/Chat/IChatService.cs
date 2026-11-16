namespace DotNextAI.Application.Chat;

/// <summary>
/// Sends questions to the language model through the configured LiteLLM proxy. Used by the CLI and, from backlog step
/// 04, by the API, so both front ends share one implementation. The service is stateless: the caller keeps the
/// conversation (see <see cref="ChatConversation"/>) and passes its history with every question.
/// </summary>
public interface IChatService
{
    /// <summary>Whether the proxy endpoint, API key and default model are configured.</summary>
    bool IsConfigured { get; }

    /// <summary>Asks a question to the default model, after the previous turns of the conversation.</summary>
    /// <param name="question">Non-empty question of at most <see cref="ChatRules.MaxQuestionLength"/> characters.</param>
    /// <param name="history">
    /// Previous turns, oldest first (empty for a single question). At most <see cref="ChatRules.MaxHistoryTurns"/>
    /// turns, none of them empty. Text only: images are sent with the question they were attached to.
    /// </param>
    /// <param name="images">Images attached to this question (step 06), validated by <see cref="ChatRules.TryValidateImages"/>.</param>
    /// <param name="cancellationToken">Cancels the call (Ctrl+C in the CLI, an aborted request in the API).</param>
    /// <exception cref="ArgumentException">The question, the history or the images break the rules.</exception>
    /// <exception cref="ChatServiceException">The model could not answer; <see cref="ChatServiceException.Failure"/> says why.</exception>
    /// <exception cref="OperationCanceledException">The caller cancelled the call.</exception>
    Task<ChatAnswer> AskAsync(
        string question,
        IReadOnlyList<ChatTurn> history,
        IReadOnlyList<ChatImage> images,
        CancellationToken cancellationToken);

    /// <summary>
    /// Same as <see cref="AskAsync(string, IReadOnlyList{ChatTurn}, IReadOnlyList{ChatImage}, CancellationToken)"/>, but
    /// the answer arrives in pieces as the model writes it (step 07). Validation and every failure happen while
    /// iterating: the rules and the proxy's refusals on the first <c>MoveNextAsync</c>, a broken connection later.
    /// The last update carries the whole answer (<see cref="ChatStreamUpdate.Completed"/>).
    /// </summary>
    IAsyncEnumerable<ChatStreamUpdate> AskStreamingAsync(
        string question,
        IReadOnlyList<ChatTurn> history,
        IReadOnlyList<ChatImage> images,
        CancellationToken cancellationToken);
}

public static class ChatServiceExtensions
{
    /// <summary>Asks a question after the previous turns, with no images.</summary>
    public static Task<ChatAnswer> AskAsync(
        this IChatService chat,
        string question,
        IReadOnlyList<ChatTurn> history,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(chat);
        return chat.AskAsync(question, history, [], cancellationToken);
    }

    /// <summary>Asks a single question, with no conversation history.</summary>
    public static Task<ChatAnswer> AskAsync(this IChatService chat, string question, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(chat);
        return chat.AskAsync(question, [], [], cancellationToken);
    }
}
