namespace DotNextAI.Application.Conversations;

/// <summary>
/// The icons and colours a folder may have (docs/specs/feature-saved-chats.md#rules). Stored as these keys; the web app
/// maps them to Fluent UI icons and theme palette colours, mirrored in <c>frontend/src/lib/chat/folderAppearance.ts</c>.
/// </summary>
public static class FolderAppearance
{
    public const string DefaultIcon = "folder";

    /// <summary>The neutral colour of the navigation's text.</summary>
    public const string DefaultColor = "default";

    public static IReadOnlyList<string> Icons { get; } =
        ["folder", "briefcase", "star", "heart", "book", "code", "lightbulb", "rocket", "people", "home", "beaker", "airplane"];

    public static IReadOnlyList<string> Colors { get; } =
        ["default", "red", "orange", "yellow", "green", "teal", "blue", "purple", "berry", "pink", "brown"];

    public static bool IsIcon(string? value) => value is not null && Icons.Contains(value, StringComparer.Ordinal);

    public static bool IsColor(string? value) => value is not null && Colors.Contains(value, StringComparer.Ordinal);
}
