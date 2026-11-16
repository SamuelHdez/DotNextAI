using DotNextAI.Application.Chat;
using DotNextAI.UnitTests.TestSupport;

namespace DotNextAI.UnitTests.Chat;

public class ImageRulesTests
{
    public static TheoryData<byte[], string> Formats => new()
    {
        { TestImages.Png, ImageFormats.Png },
        { TestImages.Jpeg, ImageFormats.Jpeg },
        { TestImages.Gif, ImageFormats.Gif },
        { TestImages.WebP, ImageFormats.WebP },
    };

    [Theory]
    [MemberData(nameof(Formats))]
    public void Detect_SupportedImage_ReturnsItsMediaType(byte[] data, string mediaType)
    {
        Assert.Equal(mediaType, ImageFormats.Detect(data));
    }

    [Fact]
    public void Detect_NotAnImage_ReturnsNull()
    {
        Assert.Null(ImageFormats.Detect(TestImages.Text));
        Assert.Null(ImageFormats.Detect([]));
        Assert.Null(ImageFormats.Detect("RIFF"u8));
    }

    [Fact]
    public void TryValidateImages_ValidImages_AreAccepted()
    {
        ChatImage[] images = [new(ImageFormats.Png, TestImages.Png), new(ImageFormats.Jpeg, TestImages.Jpeg)];

        Assert.True(ChatRules.TryValidateImages(images, out var error));
        Assert.Null(error);
    }

    [Fact]
    public void TryValidateImages_MediaTypeIsCaseInsensitive()
    {
        Assert.True(ChatRules.TryValidateImages([new ChatImage("IMAGE/PNG", TestImages.Png)], out _));
    }

    [Fact]
    public void TryValidateImages_TooMany_IsRejected()
    {
        var images = Enumerable.Range(0, ChatRules.MaxImagesPerQuestion + 1).Select(_ => new ChatImage(ImageFormats.Png, TestImages.Png)).ToArray();

        Assert.False(ChatRules.TryValidateImages(images, out var error));
        Assert.StartsWith("Too many images", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryValidateImages_TooLarge_IsRejected()
    {
        var data = new byte[ChatRules.MaxImageBytes + 1];
        TestImages.Png.CopyTo(data, 0);

        Assert.False(ChatRules.TryValidateImages([new ChatImage(ImageFormats.Png, data)], out var error));
        Assert.Contains("too large", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryValidateImages_NotAnImage_IsRejected()
    {
        Assert.False(ChatRules.TryValidateImages([new ChatImage(ImageFormats.Png, TestImages.Text)], out var error));
        Assert.Equal("Image 1 is not a PNG, JPEG, WebP or GIF image.", error);
    }

    [Fact]
    public void TryValidateImages_DeclaredTypeDifferentFromTheContent_IsRejected()
    {
        Assert.False(ChatRules.TryValidateImages([new ChatImage(ImageFormats.Jpeg, TestImages.Png)], out var error));
        Assert.Contains("declared as 'image/jpeg' but its content is image/png", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryValidateImages_EmptyImage_IsRejected()
    {
        Assert.False(ChatRules.TryValidateImages([new ChatImage(ImageFormats.Png, ReadOnlyMemory<byte>.Empty)], out var error));
        Assert.Equal("Image 1 is empty.", error);
    }
}
