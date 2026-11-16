using System.Diagnostics.CodeAnalysis;

namespace DotNextAI.Application.Conversations;

/// <summary>Rules of saved conversations and folders (docs/specs/feature-saved-chats.md).</summary>
public static class ConversationRules
{
    public const int MaxFolderNameLength = 100;

    public const int MaxTitleLength = 200;

    /// <summary>Length of the title taken from the first question when none is given.</summary>
    public const int AutoTitleLength = 60;

    public const string UntitledTitle = "New conversation";

    public static bool TryNormalizeFolderName(string? name, out string normalized, [NotNullWhen(false)] out string? error) =>
        TryNormalize(name, MaxFolderNameLength, "folder name", out normalized, out error);

    public static bool TryNormalizeTitle(string? title, out string normalized, [NotNullWhen(false)] out string? error) =>
        TryNormalize(title, MaxTitleLength, "title", out normalized, out error);

    /// <summary>
    /// The title of a conversation from its first question: the first line, whitespace collapsed, cut at a word
    /// boundary near <see cref="AutoTitleLength"/> characters with an ellipsis.
    /// </summary>
    public static string TitleFrom(string question)
    {
        ArgumentNullException.ThrowIfNull(question);
        var firstLine = question.Trim().Split('\n', 2)[0];
        var text = string.Join(' ', firstLine.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (text.Length == 0)
        {
            return UntitledTitle;
        }

        if (text.Length <= AutoTitleLength)
        {
            return text;
        }

        var cut = text.LastIndexOf(' ', AutoTitleLength);
        return string.Concat(text.AsSpan(0, cut > AutoTitleLength / 2 ? cut : AutoTitleLength).TrimEnd(), "…");
    }

    private static bool TryNormalize(string? value, int max, string what, out string normalized, [NotNullWhen(false)] out string? error)
    {
        normalized = value?.Trim() ?? string.Empty;
        error = normalized.Length switch
        {
            0 => $"The {what} is empty.",
            _ when normalized.Length > max => $"The {what} is too long ({normalized.Length} characters; the maximum is {max}).",
            _ when normalized.Any(char.IsControl) => $"The {what} contains control characters.",
            _ => null,
        };

        if (error is null)
        {
            return true;
        }

        normalized = string.Empty;
        return false;
    }
}
