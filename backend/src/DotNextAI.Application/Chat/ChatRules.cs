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

    /// <summary>Trims the question and checks it against the rules.</summary>
    /// <exception cref="ArgumentException">The question is empty or longer than <see cref="MaxQuestionLength"/>.</exception>
    public static string NormalizeQuestion(string? question) =>
        TryNormalizeQuestion(question, out var normalized, out var error)
            ? normalized
            : throw new ArgumentException(error, nameof(question));
}
