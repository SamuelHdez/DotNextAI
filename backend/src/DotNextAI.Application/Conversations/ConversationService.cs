using System.Runtime.CompilerServices;
using System.Text;
using DotNextAI.Application.Chat;

namespace DotNextAI.Application.Conversations;

/// <summary>A piece of a streamed answer in a saved conversation; the last one also has the saved conversation.</summary>
public sealed record ConversationStreamUpdate(ChatStreamUpdate Update, ConversationSummary? Saved = null);

/// <summary>
/// Questions in saved conversations: the history comes from the store instead of the client, and every answered
/// question is saved with its images and its answer (docs/specs/feature-saved-chats.md).
/// </summary>
public sealed class ConversationService(IConversationStore store, IChatService chat)
{
    /// <summary>
    /// Streams the answer and saves the exchange. A complete answer is saved when it arrives. A stopped one (the
    /// caller cancelled or stopped reading) is saved with the text so far, marked as stopped. A failed one is not
    /// saved, and the question can be asked again.
    /// </summary>
    /// <exception cref="ConversationStoreException">The conversation does not exist or is full.</exception>
    /// <exception cref="ArgumentException">The question or the images break the rules.</exception>
    /// <exception cref="ChatServiceException">The model could not answer.</exception>
    public async IAsyncEnumerable<ConversationStreamUpdate> AskStreamingAsync(
        Guid conversationId,
        string question,
        IReadOnlyList<ChatImage> images,
        string? model,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(images);
        var conversation = await store.GetConversationAsync(conversationId, cancellationToken)
            ?? throw new ConversationStoreException(StoreFailure.NotFound, "The conversation does not exist.");
        if (conversation.Summary.QuestionCount >= ChatRules.MaxConversationQuestions)
        {
            throw new ConversationStoreException(
                StoreFailure.ConversationFull,
                $"The conversation reached the limit of {ChatRules.MaxConversationQuestions} questions.");
        }

        var prompt = ChatRules.NormalizeQuestion(question);
        ChatTurn[] history = [.. conversation.Messages.Select(message => new ChatTurn(message.Role, message.Text))];
        // The first question names a conversation that still has the default title.
        var title = conversation.Messages.Count == 0 && conversation.Summary.Title == ConversationRules.UntitledTitle
            ? ConversationRules.TitleFrom(prompt)
            : null;

        var answer = new StringBuilder();
        var finished = false;
        await using var updates = chat
            .AskStreamingAsync(new ChatRequest(prompt, history, images, model), cancellationToken)
            .GetAsyncEnumerator(cancellationToken);
        try
        {
            while (true)
            {
                try
                {
                    if (!await updates.MoveNextAsync())
                    {
                        break;
                    }
                }
                catch (ChatServiceException)
                {
                    // Nothing is saved for a failed answer.
                    finished = true;
                    throw;
                }

                var update = updates.Current;
                if (update.Completed is { } completed)
                {
                    finished = true;
                    var saved = await store.AppendExchangeAsync(
                        conversationId,
                        new SavedExchange(prompt, images, completed.Text, completed.Model, completed.Usage, completed.Truncated, Stopped: false),
                        title,
                        CancellationToken.None);
                    yield return new ConversationStreamUpdate(update, saved);
                    yield break;
                }

                answer.Append(update.Text);
                yield return new ConversationStreamUpdate(update);
            }
        }
        finally
        {
            // Reached when the caller stops early, including a closed connection that never throws here.
            if (!finished && answer.Length > 0)
            {
                await store.AppendExchangeAsync(
                    conversationId,
                    new SavedExchange(prompt, images, answer.ToString(), model, Usage: null, Truncated: false, Stopped: true),
                    title,
                    CancellationToken.None);
            }
        }
    }
}
