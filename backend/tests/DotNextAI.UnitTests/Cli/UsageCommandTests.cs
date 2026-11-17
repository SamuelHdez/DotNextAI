using DotNextAI.Application.Chat;
using DotNextAI.Application.Images;
using static DotNextAI.UnitTests.Cli.CliApplicationTests;

namespace DotNextAI.UnitTests.Cli;

/// <summary>Token metrics in the terminal app (step 10).</summary>
public class UsageCommandTests
{
    private static string Lines(params string[] lines) => string.Join("\n", lines) + "\n";

    private static FakeChatService Counting() =>
        new(q => new ChatAnswer($"answer to {q}", "gpt-5.6-luna", false, new TokenUsage(1_200, 34, 1_234)));

    [Fact]
    public async Task Ask_PrintsTheTokensOnStderrAndKeepsStdoutForTheAnswer()
    {
        var run = await RunAsync(Counting(), ["ask", "Hi"]);

        Assert.Equal("answer to Hi" + Environment.NewLine, run.Out);
        Assert.Contains("Tokens: 1,200 in · 34 out · 1,234 total", run.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("0.000137", "$0.00014")]
    [InlineData("0.0123", "$0.012")]
    [InlineData("0.25", "$0.25")]
    [InlineData("0.5", "$0.50")]
    [InlineData("1.234", "$1.23")]
    [InlineData("12", "$12.00")]
    [InlineData("0", "$0.00")]
    public void Cost_HasTwoSignificantDigitsBelowADollarAndTwoDecimalsFromOne(string cost, string expected)
    {
        Assert.Equal(expected, global::DotNextAI.Cli.CliMessages.Cost(decimal.Parse(cost, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public async Task Ask_PricedModel_AddsTheCostToTheTokenLine()
    {
        var chat = new FakeChatService(_ => new ChatAnswer("pong", "gpt-6-luna", false, new TokenUsage(1_200, 34, 1_234, 0.000137m)));

        var run = await RunAsync(chat, ["ask", "Hi"]);

        Assert.Contains("Tokens: 1,200 in · 34 out · 1,234 total · $0.00014", run.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Chat_PricedModel_ShowsTheCostOfEachAnswerAndOfTheConversation()
    {
        var chat = new FakeChatService(_ => new ChatAnswer("pong", "gpt-6-luna", false, new TokenUsage(1_200, 34, 1_234, 0.00015m)));

        var run = await RunAsync(chat, ["chat"], stdin: Lines("one", "two", "/usage"), inputRedirected: true);

        Assert.Contains("Tokens: 1,200 in · 34 out · 1,234 total · $0.00015 (conversation: 2,468 · $0.00030)", run.Error, StringComparison.Ordinal);
        Assert.Contains("This conversation: 2 answers, 2,400 in · 68 out · 2,468 total tokens, $0.00030 at the LiteLLM proxy's prices.", run.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ask_ProxyWithoutUsage_PrintsNoTokenLine()
    {
        var run = await RunAsync(new FakeChatService(), ["ask", "Hi"]);

        Assert.DoesNotContain("Tokens:", run.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Chat_PrintsEachAnswersTokensAndTheConversationTotal()
    {
        var run = await RunAsync(Counting(), ["chat"], stdin: Lines("one", "two"), inputRedirected: true);

        Assert.Contains("Tokens: 1,200 in · 34 out · 1,234 total (conversation: 1,234)", run.Error, StringComparison.Ordinal);
        Assert.Contains("Tokens: 1,200 in · 34 out · 1,234 total (conversation: 2,468)", run.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Chat_UsageCommand_ShowsTheConversationTotal()
    {
        var run = await RunAsync(Counting(), ["chat"], stdin: Lines("/usage", "one", "two", "/usage", "/new", "/usage"), inputRedirected: true);

        Assert.Contains("No questions answered yet.", run.Error, StringComparison.Ordinal);
        Assert.Contains("This conversation: 2 answers, 2,400 in · 68 out · 2,468 total tokens.", run.Error, StringComparison.Ordinal);
        Assert.Equal(2, run.Error.Split("No questions answered yet.").Length - 1);
    }

    [Fact]
    public async Task Chat_UsageCommand_SaysWhenTheProxyReportsNothing()
    {
        var run = await RunAsync(new FakeChatService(), ["chat"], stdin: Lines("one", "/usage"), inputRedirected: true);

        Assert.Contains("1 answers; the LiteLLM proxy did not report their tokens.", run.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Image_PrintsTheTokensOfTheGeneration()
    {
        var folder = Directory.CreateTempSubdirectory("dotnextai-usage-").FullName;
        try
        {
            var images = new FakeImageService
            {
                Result = new GeneratedImage(new ChatImage(ImageFormats.Png, FakeImageService.Png), "gpt-image-2", null, new TokenUsage(12, 4_160, 4_172)),
            };

            var run = await RunAsync(new FakeChatService(), ["image", "x", "-o", Path.Combine(folder, "x.png")], images: images);

            Assert.Contains("Tokens: 12 in · 4,160 out · 4,172 total", run.Error, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
