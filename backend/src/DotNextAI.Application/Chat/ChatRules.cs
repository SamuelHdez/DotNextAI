using System.Diagnostics.CodeAnalysis;

namespace DotNextAI.Application.Chat;

/// <summary>Input rules shared by every front end (see docs/specs/feature-llm-chat.md).</summary>
public static class ChatRules
{
    /// <summary>
    /// Maximum question length in characters. It keeps an accidental paste or pipe of a large file from being sent
    /// (and billed) as one prompt.
    /// </summary>
    public const int MaxQuestionLength = 20_000;

    /// <summary>
    /// Maximum questions in one conversation. Every question resends the whole history, so cost and latency grow with
    /// it; at the limit the user starts a new conversation instead of losing old turns silently.
    /// </summary>
    public const int MaxConversationQuestions = 50;

    /// <summary>Maximum history turns sent with a question (a question and its answer are two turns).</summary>
    public const int MaxHistoryTurns = 2 * MaxConversationQuestions;

    /// <summary>Trims the question and checks it against the rules.</summary>
    /// <param name="question">Raw question.</param>
    /// <param name="normalized">Trimmed question when valid; empty otherwise.</param>
    /// <param name="error">User-facing reason when invalid.</param>
    public static bool TryNormalizeQuestion(
        string? question,
        out string normalized,
        [NotNullWhen(false)] out string? error)
    {
        normalized = question?.Trim() ?? string.Empty;
        error = normalized.Length switch
        {
            0 => "The question is empty.",
            > MaxQuestionLength =>
                $"The question is too long ({normalized.Length:N0} characters; the maximum is {MaxQuestionLength:N0}).",
            _ => null,
        };

        if (error is null)
        {
            return true;
        }

        normalized = string.Empty;
        return false;
    }

    /// <summary>Checks the history passed with a question.</summary>
    /// <exception cref="ArgumentException">More than <see cref="MaxHistoryTurns"/> turns, or an empty turn.</exception>
    public static void ValidateHistory(IReadOnlyList<ChatTurn> history)
    {
        ArgumentNullException.ThrowIfNull(history);
        if (history.Count > MaxHistoryTurns)
        {
            throw new ArgumentException(
                $"The conversation is too long ({history.Count} turns; the maximum is {MaxHistoryTurns}).",
                nameof(history));
        }

        if (history.Any(turn => turn is null || string.IsNullOrWhiteSpace(turn.Text)))
        {
            throw new ArgumentException("The conversation contains an empty turn.", nameof(history));
        }
    }

    /// <summary>Trims the question and checks it against the rules.</summary>
    /// <exception cref="ArgumentException">The question is empty or longer than <see cref="MaxQuestionLength"/>.</exception>
    public static string NormalizeQuestion(string? question) =>
        TryNormalizeQuestion(question, out var normalized, out var error)
            ? normalized
            : throw new ArgumentException(error, nameof(question));
}
