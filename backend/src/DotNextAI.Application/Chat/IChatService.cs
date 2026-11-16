namespace DotNextAI.Application.Chat;

/// <summary>
/// Sends questions to the language model through the configured LiteLLM proxy. Used by the CLI and the API, so both
/// front ends share one implementation. The service is stateless: the caller keeps the conversation (see
/// <see cref="ChatConversation"/>) and passes its history with every question.
/// </summary>
public interface IChatService
{
    /// <summary>Whether the proxy endpoint, API key and default model are configured.</summary>
    bool IsConfigured { get; }

    /// <summary>Asks a question after the previous turns of the conversation and returns the whole answer.</summary>
    /// <param name="request">
    /// The question, its history (at most <see cref="ChatRules.MaxHistoryTurns"/> turns, text only), its images
    /// (<see cref="ChatRules.TryValidateImages"/>) and the model (null for the default).
    /// </param>
    /// <param name="cancellationToken">Cancels the call (Ctrl+C in the CLI, an aborted request in the API).</param>
    /// <exception cref="ArgumentException">The question, the history or the images break the rules.</exception>
    /// <exception cref="ChatServiceException">
    /// The model could not answer, or the requested model is not a chat model of the proxy
    /// (<see cref="ChatFailure.ModelNotFound"/>); <see cref="ChatServiceException.Failure"/> says why.
    /// </exception>
    /// <exception cref="OperationCanceledException">The caller cancelled the call.</exception>
    Task<ChatAnswer> AskAsync(ChatRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Same as <see cref="AskAsync"/>, but the answer arrives in pieces as the model writes it (step 07). Validation and
    /// every failure happen while iterating: the rules and the proxy's refusals on the first <c>MoveNextAsync</c>, a
    /// broken connection later. The last update carries the whole answer (<see cref="ChatStreamUpdate.Completed"/>).
    /// </summary>
    IAsyncEnumerable<ChatStreamUpdate> AskStreamingAsync(ChatRequest request, CancellationToken cancellationToken);
}

public static class ChatServiceExtensions
{
    /// <summary>Asks a single question to the default model, with no history and no images.</summary>
    public static Task<ChatAnswer> AskAsync(this IChatService chat, string question, CancellationToken cancellationToken) =>
        AskAsync(chat, question, [], [], cancellationToken);

    /// <summary>Asks a question after the previous turns, to the default model, with no images.</summary>
    public static Task<ChatAnswer> AskAsync(
        this IChatService chat,
        string question,
        IReadOnlyList<ChatTurn> history,
        CancellationToken cancellationToken) =>
        AskAsync(chat, question, history, [], cancellationToken);

    /// <summary>Asks a question with its history and images, to the default model.</summary>
    public static Task<ChatAnswer> AskAsync(
        this IChatService chat,
        string question,
        IReadOnlyList<ChatTurn> history,
        IReadOnlyList<ChatImage> images,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(chat);
        return chat.AskAsync(new ChatRequest(question, history, images), cancellationToken);
    }

    /// <summary>Streams the answer to a question with its history and images, from the default model.</summary>
    public static IAsyncEnumerable<ChatStreamUpdate> AskStreamingAsync(
        this IChatService chat,
        string question,
        IReadOnlyList<ChatTurn> history,
        IReadOnlyList<ChatImage> images,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(chat);
        return chat.AskStreamingAsync(new ChatRequest(question, history, images), cancellationToken);
    }
}
