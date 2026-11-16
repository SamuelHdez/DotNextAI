using System.Diagnostics.CodeAnalysis;
using DotNextAI.Application.Chat;

namespace DotNextAI.Cli;

/// <summary>Loads image files for the CLI (backlog step 06); the format comes from the bytes, not the extension.</summary>
internal static class ImageFiles
{
    public static bool TryLoad(
        string path,
        [NotNullWhen(true)] out ChatImage? image,
        [NotNullWhen(false)] out string? error)
    {
        image = null;
        var name = Path.GetFileName(path);
        FileInfo file;
        try
        {
            file = new FileInfo(path.Trim().Trim('"'));
        }
        catch (ArgumentException)
        {
            error = $"'{path}' is not a valid file path.";
            return false;
        }

        if (!file.Exists)
        {
            error = $"Image not found: {path}";
            return false;
        }

        // Checked before reading, so a huge file is never loaded into memory.
        if (file.Length > ChatRules.MaxImageBytes)
        {
            error = $"{name} is too large ({file.Length / 1024 / 1024.0:F1} MB; the maximum is {ChatRules.MaxImageBytes / 1024 / 1024} MB).";
            return false;
        }

        byte[] data;
        try
        {
            data = File.ReadAllBytes(file.FullName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = $"{name} could not be read: {ex.Message}";
            return false;
        }

        var mediaType = ImageFormats.Detect(data);
        if (mediaType is null)
        {
            error = $"{name} is not a PNG, JPEG, WebP or GIF image.";
            return false;
        }

        image = new ChatImage(mediaType, data);
        error = null;
        return true;
    }

    public static string Describe(ChatImage image, string path)
    {
        var length = image.Data.Length;
        var size = length < 1024 ? $"{length} bytes" : $"{length / 1024.0:F0} KB";
        return $"{Path.GetFileName(path)} ({image.MediaType}, {size})";
    }
}
