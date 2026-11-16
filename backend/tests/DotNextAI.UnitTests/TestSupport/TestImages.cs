namespace DotNextAI.UnitTests.TestSupport;

/// <summary>Small real image files for the tests.</summary>
internal static class TestImages
{
    /// <summary>A 1×1 PNG.</summary>
    public static byte[] Png { get; } = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    public static byte[] Jpeg { get; } = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F'];

    public static byte[] Gif { get; } = "GIF89a\u0001\u0000\u0001\u0000"u8.ToArray();

    public static byte[] WebP { get; } = [.. "RIFF"u8, 0x24, 0, 0, 0, .. "WEBPVP8 "u8];

    public static byte[] Text { get; } = "This is not an image."u8.ToArray();
}
