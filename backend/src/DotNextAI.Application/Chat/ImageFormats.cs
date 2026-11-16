namespace DotNextAI.Application.Chat;

/// <summary>
/// Image formats the model accepts, recognised by their file signature ("magic bytes"), never by a file extension or a
/// media type declared by the client.
/// </summary>
public static class ImageFormats
{
    public const string Png = "image/png";
    public const string Jpeg = "image/jpeg";
    public const string WebP = "image/webp";
    public const string Gif = "image/gif";

    public static IReadOnlyList<string> Supported { get; } = [Png, Jpeg, WebP, Gif];

    /// <summary>The media type of the bytes, or null when they are not a supported image.</summary>
    public static string? Detect(ReadOnlySpan<byte> data)
    {
        if (data.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return Png;
        }

        if (data.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]))
        {
            return Jpeg;
        }

        if (data.StartsWith("GIF87a"u8) || data.StartsWith("GIF89a"u8))
        {
            return Gif;
        }

        if (data.Length >= 12 && data.StartsWith("RIFF"u8) && data[8..12].SequenceEqual("WEBP"u8))
        {
            return WebP;
        }

        return null;
    }
}
