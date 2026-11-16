using DotNextAI.Application.Chat;
using DotNextAI.Application.Images;
using DotNextAI.UnitTests.TestSupport;
using static DotNextAI.UnitTests.Cli.CliApplicationTests;

namespace DotNextAI.UnitTests.Cli;

/// <summary>`dotnextai image` (step 09).</summary>
public sealed class ImageGenerationCommandTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("dotnextai-generated-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private string PathFor(string name) => Path.Combine(_folder, name);

    [Fact]
    public async Task Image_SavesTheFileAndPrintsItsPath()
    {
        var images = new FakeImageService();
        var output = PathFor("cat.png");

        var run = await RunAsync(new FakeChatService(), ["image", "A", "cat", "in", "a", "hat", "--output", output], images: images);

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(Path.GetFullPath(output) + Environment.NewLine, run.Out);
        Assert.Equal(FakeImageService.Png, await File.ReadAllBytesAsync(output, TestContext.Current.CancellationToken));
        Assert.Contains("The model drew: A tiny black pixel.", run.Error, StringComparison.Ordinal);
        var request = Assert.Single(images.Requests);
        Assert.Equal("A cat in a hat", request.Prompt);
        Assert.Equal(ImageSize.Square, request.Size);
    }

    [Theory]
    [InlineData("landscape", ImageSize.Landscape)]
    [InlineData("Portrait", ImageSize.Portrait)]
    public async Task Image_SizeOption_IsPassed(string value, ImageSize size)
    {
        var images = new FakeImageService();

        await RunAsync(new FakeChatService(), ["image", "x", "-s", value, "-o", PathFor("x.png")], images: images);

        Assert.Equal(size, Assert.Single(images.Requests).Size);
    }

    [Fact]
    public async Task Image_ExistingFile_IsNotOverwrittenAndNothingIsGenerated()
    {
        var images = new FakeImageService();
        var output = PathFor("keep.png");
        await File.WriteAllTextAsync(output, "mine", TestContext.Current.CancellationToken);

        var run = await RunAsync(new FakeChatService(), ["image", "x", "-o", output], images: images);

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("already exists. Use --force", run.Error, StringComparison.Ordinal);
        Assert.Equal("mine", await File.ReadAllTextAsync(output, TestContext.Current.CancellationToken));
        Assert.Empty(images.Requests);
    }

    [Fact]
    public async Task Image_ForceOverwritesTheFile()
    {
        var output = PathFor("again.png");
        await File.WriteAllTextAsync(output, "old", TestContext.Current.CancellationToken);

        var run = await RunAsync(new FakeChatService(), ["image", "x", "-o", output, "--force"]);

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(FakeImageService.Png, await File.ReadAllBytesAsync(output, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Image_ExtensionFollowsTheRealFormat()
    {
        var images = new FakeImageService { Result = new GeneratedImage(new ChatImage(ImageFormats.Jpeg, TestImages.Jpeg), "gpt-image-2", null) };

        var run = await RunAsync(new FakeChatService(), ["image", "x", "-o", PathFor("photo.png")], images: images);

        Assert.Equal(Path.GetFullPath(PathFor("photo.jpg")) + Environment.NewLine, run.Out);
        Assert.True(File.Exists(PathFor("photo.jpg")));
    }

    [Fact]
    public async Task Image_Failure_ExitsWithModelErrorAndAHint()
    {
        var images = new FakeImageService { Failure = new ChatServiceException(ChatFailure.ModelUnavailable, "The model 'gpt-image-2' is currently unavailable.") };

        var run = await RunAsync(new FakeChatService(), ["image", "x", "-o", PathFor("x.png")], images: images);

        Assert.Equal(1, run.ExitCode);
        Assert.Contains("Error: The model 'gpt-image-2' is currently unavailable.", run.Error, StringComparison.Ordinal);
        Assert.Contains("Try again later", run.Error, StringComparison.Ordinal);
        Assert.False(File.Exists(PathFor("x.png")));
    }

    [Fact]
    public async Task Image_NotConfigured_ExitsWith3()
    {
        var run = await RunAsync(new FakeChatService(), ["image", "x"], images: new FakeImageService { IsConfigured = false });

        Assert.Equal(3, run.ExitCode);
    }

    [Fact]
    public async Task Image_WithoutDescription_IsAUsageError()
    {
        var images = new FakeImageService();

        var run = await RunAsync(new FakeChatService(), ["image"], images: images);

        Assert.Equal(2, run.ExitCode);
        Assert.Empty(images.Requests);
    }

    [Fact]
    public async Task Image_TooLongDescription_IsAUsageError()
    {
        var run = await RunAsync(new FakeChatService(), ["image", new string('a', ImageRules.MaxPromptLength + 1)]);

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("too long", run.Error, StringComparison.Ordinal);
    }
}
