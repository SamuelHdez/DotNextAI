using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;
using DotNextAI.Application.Chat;

namespace DotNextAI.Api.Endpoints;

/// <summary>Server-Sent Events of POST /api/chat/stream (docs/design/api.md#post-apichatstream).</summary>
internal static class ChatStreamEvents
{
    public const string Delta = "delta";
    public const string Done = "done";
    public const string Error = "error";

    /// <summary>Relays the updates whose first one was already read (<paramref name="hasCurrent"/>), then disposes them.</summary>
    public static async IAsyncEnumerable<SseItem<string>> From(
        IAsyncEnumerator<ChatStreamUpdate> updates,
        bool hasCurrent,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using (updates)
        {
            ChatServiceException? failure = null;
            while (hasCurrent)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var update = updates.Current;
                yield return update.Completed is { } answer
                    ? Item(Done, new { answer = answer.Text, model = answer.Model, truncated = answer.Truncated, usage = Contracts.TokenUsageDto.From(answer.Usage) })
                    : Item(Delta, new { text = update.Text });

                try
                {
                    hasCurrent = await updates.MoveNextAsync();
                }
                catch (ChatServiceException ex)
                {
                    // The 200 headers are already sent: the failure travels as the last event, with its failure code.
                    failure = ex;
                    hasCurrent = false;
                }
            }

            if (failure is not null)
            {
                yield return Item(Error, new
                {
                    status = ChatProblems.StatusCode(failure.Failure),
                    failure = failure.Failure.ToString(),
                    title = ChatProblems.Title(failure.Failure),
                    detail = failure.Message,
                });
            }
        }
    }

    /// <summary>
    /// Same events for a saved conversation (POST /api/conversations/{id}/messages/stream); <c>done</c> also carries the
    /// saved conversation (new title, totals).
    /// </summary>
    public static async IAsyncEnumerable<SseItem<string>> FromConversation(
        IAsyncEnumerator<DotNextAI.Application.Conversations.ConversationStreamUpdate> updates,
        bool hasCurrent,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using (updates)
        {
            ChatServiceException? failure = null;
            while (hasCurrent)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var (update, saved) = updates.Current;
                yield return update.Completed is { } answer
                    ? Item(Done, new
                    {
                        answer = answer.Text,
                        model = answer.Model,
                        truncated = answer.Truncated,
                        usage = Contracts.TokenUsageDto.From(answer.Usage),
                        conversation = saved is null ? null : ConversationEndpoints.ToDto(saved),
                    })
                    : Item(Delta, new { text = update.Text });

                try
                {
                    hasCurrent = await updates.MoveNextAsync();
                }
                catch (ChatServiceException ex)
                {
                    failure = ex;
                    hasCurrent = false;
                }
            }

            if (failure is not null)
            {
                yield return Item(Error, new
                {
                    status = ChatProblems.StatusCode(failure.Failure),
                    failure = failure.Failure.ToString(),
                    title = ChatProblems.Title(failure.Failure),
                    detail = failure.Message,
                });
            }
        }
    }

    private static SseItem<string> Item(string eventType, object data) =>
        new(JsonSerializer.Serialize(data, JsonSerializerOptions.Web), eventType);
}
