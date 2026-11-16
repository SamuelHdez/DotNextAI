namespace DotNextAI.Application.Chat;

/// <summary>
/// A multi-turn conversation kept by the front end (backlog step 03). It sends its history with every question and
/// records a turn only when the model answered, so a failed question can simply be asked again.
/// </summary>
public sealed class ChatConversation(IChatService chat)
{
    private readonly List<ChatTurn> _turns = [];

    /// <summary>Questions and answers so far, oldest first.</summary>
    public IReadOnlyList<ChatTurn> Turns => _turns;

    /// <summary>Questions answered in this conversation.</summary>
    public int QuestionCount => _turns.Count / 2;

    /// <summary>True when <see cref="ChatRules.MaxConversationQuestions"/> is reached: start a new conversation.</summary>
    public bool IsFull => QuestionCount >= ChatRules.MaxConversationQuestions;

    /// <summary>Asks the question after the previous turns and records both on success.</summary>
    /// <exception cref="InvalidOperationException">The conversation is full (<see cref="IsFull"/>).</exception>
    /// <exception cref="ArgumentException">The question breaks the rules.</exception>
    /// <exception cref="ChatServiceException">The model could not answer; the conversation is unchanged.</exception>
    public Task<ChatAnswer> AskAsync(string question, CancellationToken cancellationToken) =>
        AskAsync(question, [], cancellationToken);

    /// <summary>
    /// Chat model for the next questions (step 08), or null for the default. It can change in the middle of a
    /// conversation: the history goes to whichever model answers next.
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Asks the question with its images after the previous turns. The history keeps the text only, so later questions
    /// see the model's earlier answer about an image, not the image itself (docs/specs/feature-llm-chat.md#images).
    /// </summary>
    public async Task<ChatAnswer> AskAsync(string question, IReadOnlyList<ChatImage> images, CancellationToken cancellationToken)
    {
        if (IsFull)
        {
            throw new InvalidOperationException(
                $"The conversation reached the limit of {ChatRules.MaxConversationQuestions} questions.");
        }

        var normalized = ChatRules.NormalizeQuestion(question);
        var answer = await chat.AskAsync(new ChatRequest(normalized, _turns.ToArray(), images, Model), cancellationToken).ConfigureAwait(false);

        _turns.Add(new ChatTurn(ChatTurnRole.User, normalized));
        _turns.Add(new ChatTurn(ChatTurnRole.Assistant, answer.Text));
        return answer;
    }

    /// <summary>
    /// Streams the answer to the question (step 07) and records both turns when the answer is complete. Stopping the
    /// iteration early (the user pressed Stop) or a failure records nothing.
    /// </summary>
    /// <exception cref="InvalidOperationException">The conversation is full (<see cref="IsFull"/>).</exception>
    public async IAsyncEnumerable<ChatStreamUpdate> AskStreamingAsync(
        string question,
        IReadOnlyList<ChatImage> images,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (IsFull)
        {
            throw new InvalidOperationException(
                $"The conversation reached the limit of {ChatRules.MaxConversationQuestions} questions.");
        }

        var normalized = ChatRules.NormalizeQuestion(question);
        var history = _turns.ToArray();
        await foreach (var update in chat.AskStreamingAsync(new ChatRequest(normalized, history, images, Model), cancellationToken).ConfigureAwait(false))
        {
            if (update.Completed is { } answer)
            {
                _turns.Add(new ChatTurn(ChatTurnRole.User, normalized));
                _turns.Add(new ChatTurn(ChatTurnRole.Assistant, answer.Text));
            }

            yield return update;
        }
    }

    /// <summary>Forgets every turn: the next question starts a new conversation.</summary>
    public void Reset() => _turns.Clear();
}
