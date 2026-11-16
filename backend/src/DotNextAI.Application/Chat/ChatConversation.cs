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
        var answer = await chat.AskAsync(normalized, _turns.ToArray(), images, cancellationToken).ConfigureAwait(false);

        _turns.Add(new ChatTurn(ChatTurnRole.User, normalized));
        _turns.Add(new ChatTurn(ChatTurnRole.Assistant, answer.Text));
        return answer;
    }

    /// <summary>Forgets every turn: the next question starts a new conversation.</summary>
    public void Reset() => _turns.Clear();
}
