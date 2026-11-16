using DotNextAI.Application.Chat;

namespace DotNextAI.Cli;

/// <summary>Writes a streamed answer to stdout as it arrives (backlog step 07).</summary>
internal static class AnswerWriter
{
    /// <summary>Writes every piece, ends the line and returns the whole answer.</summary>
    /// <exception cref="ChatServiceException">The answer failed; a partial answer already written gets its line ended first.</exception>
    public static async Task<ChatAnswer> WriteAsync(IAsyncEnumerable<ChatStreamUpdate> updates, TextWriter output)
    {
        var partial = false;
        try
        {
            await foreach (var update in updates)
            {
                if (update.Completed is { } answer)
                {
                    await output.WriteLineAsync();
                    return answer;
                }

                await output.WriteAsync(update.Text);
                await output.FlushAsync();
                partial = true;
            }
        }
        catch
        {
            // Keeps the error (on stderr) from starting in the middle of the partial answer's line.
            if (partial)
            {
                await output.WriteLineAsync();
            }

            throw;
        }

        throw new ChatServiceException(ChatFailure.EmptyAnswer, "The answer ended before it was complete.");
    }
}
