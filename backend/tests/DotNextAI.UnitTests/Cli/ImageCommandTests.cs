using DotNextAI.Application.Chat;
using DotNextAI.UnitTests.TestSupport;
using static DotNextAI.UnitTests.Cli.CliApplicationTests;

namespace DotNextAI.UnitTests.Cli;

/// <summary>Images in the terminal app (step 06): `ask --image` and the chat's /image, /images.</summary>
public sealed class ImageCommandTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("dotnextai-images-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private string File(string name, byte[] content)
    {
        var path = Path.Combine(_folder, name);
        System.IO.File.WriteAllBytes(path, content);
        return path;
    }

    private static string Lines(params string[] lines) => string.Join("\n", lines) + "\n";

    [Fact]
    public async Task Ask_WithImages_SendsThemDetectedByContent()
    {
        var chat = new FakeChatService();
        // The extension is wrong on purpose: the format comes from the bytes.
        var png = File("photo.jpg", TestImages.Png);
        var gif = File("anim.gif", TestImages.Gif);

        var run = await RunAsync(chat, ["ask", "--image", png, "-i", gif, "What", "is", "this?"]);

        Assert.Equal(0, run.ExitCode);
        var call = Assert.Single(chat.Calls);
        Assert.Equal("What is this?", call.Question);
        Assert.Equal([ImageFormats.Png, ImageFormats.Gif], call.Images.Select(image => image.MediaType));
        Assert.Equal(TestImages.Png, call.Images[0].Data.ToArray());
    }

    [Fact]
    public async Task Ask_MissingImage_ExitsWithUsageErrorWithoutCallingTheModel()
    {
        var chat = new FakeChatService();

        var run = await RunAsync(chat, ["ask", "--image", Path.Combine(_folder, "missing.png"), "Hi"]);

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("Image not found", run.Error, StringComparison.Ordinal);
        Assert.Empty(chat.Calls);
    }

    [Fact]
    public async Task Ask_FileThatIsNotAnImage_ExitsWithUsageError()
    {
        var chat = new FakeChatService();

        var run = await RunAsync(chat, ["ask", "--image", File("notes.png", TestImages.Text), "Hi"]);

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("notes.png is not a PNG, JPEG, WebP or GIF image.", run.Error, StringComparison.Ordinal);
        Assert.Empty(chat.Calls);
    }

    [Fact]
    public async Task Ask_TooLargeImage_ExitsWithUsageErrorBeforeReadingIt()
    {
        var chat = new FakeChatService();
        var data = new byte[ChatRules.MaxImageBytes + 1];
        TestImages.Png.CopyTo(data, 0);

        var run = await RunAsync(chat, ["ask", "--image", File("big.png", data), "Hi"]);

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("big.png is too large", run.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ask_TooManyImages_ExitsWithUsageError()
    {
        var chat = new FakeChatService();
        var png = File("a.png", TestImages.Png);
        var args = new List<string> { "ask" };
        for (var i = 0; i <= ChatRules.MaxImagesPerQuestion; i++)
        {
            args.AddRange(["-i", png]);
        }

        args.Add("Hi");

        var run = await RunAsync(chat, [.. args]);

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("Too many images", run.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Chat_ImageCommand_AttachesTheImageToTheNextQuestionOnly()
    {
        var chat = new FakeChatService();
        var png = File("cat.png", TestImages.Png);

        var run = await RunAsync(chat, ["chat"], stdin: Lines($"/image {png}", "What is this?", "And now?"), inputRedirected: true);

        Assert.Equal(0, run.ExitCode);
        Assert.Contains("Attached cat.png (image/png", run.Error, StringComparison.Ordinal);
        Assert.Single(chat.Calls[0].Images);
        Assert.Empty(chat.Calls[1].Images);
    }

    [Fact]
    public async Task Chat_ImagesCommand_ListsTheAttachments()
    {
        var chat = new FakeChatService();
        var png = File("cat.png", TestImages.Png);

        var run = await RunAsync(chat, ["chat"], stdin: Lines("/images", $"/image {png}", "/images"), inputRedirected: true);

        Assert.Contains("No images attached.", run.Error, StringComparison.Ordinal);
        Assert.Contains("Attached to your next question: cat.png", run.Error, StringComparison.Ordinal);
        Assert.Empty(chat.Calls);
    }

    [Fact]
    public async Task Chat_NewCommand_DiscardsTheAttachments()
    {
        var chat = new FakeChatService();
        var png = File("cat.png", TestImages.Png);

        await RunAsync(chat, ["chat"], stdin: Lines($"/image {png}", "/new", "Hi"), inputRedirected: true);

        Assert.Empty(Assert.Single(chat.Calls).Images);
    }

    [Fact]
    public async Task Chat_FailedQuestion_KeepsTheAttachmentsForTheRetry()
    {
        var calls = 0;
        var chat = new FakeChatService(q => ++calls == 1
            ? throw new ChatServiceException(ChatFailure.Timeout, "Too slow.")
            : new ChatAnswer($"answer to {q}", "gpt-5.6-luna", Truncated: false));
        var png = File("cat.png", TestImages.Png);

        await RunAsync(chat, ["chat"], stdin: Lines($"/image {png}", "What is this?", "What is this?"), inputRedirected: true);

        Assert.Single(chat.Calls[1].Images);
    }

    [Theory]
    [InlineData("/image")]
    [InlineData("/image   ")]
    public async Task Chat_ImageCommandWithoutPath_ShowsTheUsage(string command)
    {
        var run = await RunAsync(new FakeChatService(), ["chat"], stdin: Lines(command), inputRedirected: true);

        Assert.Contains("Usage: /image <path", run.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Chat_ImageCommand_RefusesMoreThanTheLimit()
    {
        var png = File("cat.png", TestImages.Png);
        var lines = Enumerable.Repeat($"/image {png}", ChatRules.MaxImagesPerQuestion + 1).ToArray();

        var run = await RunAsync(new FakeChatService(), ["chat"], stdin: Lines(lines), inputRedirected: true);

        Assert.Contains($"You can attach up to {ChatRules.MaxImagesPerQuestion} images", run.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Chat_ImageCommand_ReportsAFileThatIsNotAnImage()
    {
        var chat = new FakeChatService();

        var run = await RunAsync(chat, ["chat"], stdin: Lines($"/image {File("notes.txt", TestImages.Text)}", "Hi"), inputRedirected: true);

        Assert.Contains("notes.txt is not a PNG, JPEG, WebP or GIF image.", run.Error, StringComparison.Ordinal);
        Assert.Empty(Assert.Single(chat.Calls).Images);
    }
}
