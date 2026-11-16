using DotNextAI.Application.Chat;
using static DotNextAI.UnitTests.Cli.CliApplicationTests;

namespace DotNextAI.UnitTests.Cli;

/// <summary>Model selection in the terminal app (step 08): `models`, `ask --model` and the chat's /model.</summary>
public class ModelCommandTests
{
    private static string Lines(params string[] lines) => string.Join("\n", lines) + "\n";

    [Fact]
    public async Task Models_ListsTheChatModelsWithTheDefaultMarked()
    {
        var run = await RunAsync(new FakeChatService(), ["models"]);

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(
            [
                "* gpt-5.6-luna   (default, 922,000 input / 128,000 output tokens)",
                "  gpt-5.6-terra  (922,000 input / 128,000 output tokens)",
            ],
            run.Out.TrimEnd().Split(Environment.NewLine));
    }

    [Fact]
    public async Task Models_ListFailure_ExitsWithModelError()
    {
        var catalog = new FakeModelCatalog { Failure = new ChatServiceException(ChatFailure.Unavailable, "The LiteLLM proxy could not be reached.") };

        var run = await RunAsync(new FakeChatService(), ["models"], catalog: catalog);

        Assert.Equal(1, run.ExitCode);
        Assert.Contains("Error: The LiteLLM proxy could not be reached.", run.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Models_NotConfigured_ExitsWith3()
    {
        var run = await RunAsync(new FakeChatService { IsConfigured = false }, ["models"]);

        Assert.Equal(3, run.ExitCode);
    }

    [Theory]
    [InlineData("--model")]
    [InlineData("-m")]
    public async Task Ask_WithModel_SendsIt(string option)
    {
        var chat = new FakeChatService();

        var run = await RunAsync(chat, ["ask", option, "gpt-5.6-terra", "Hi"]);

        Assert.Equal(0, run.ExitCode);
        Assert.Equal("gpt-5.6-terra", Assert.Single(chat.Calls).Model);
    }

    [Fact]
    public async Task Ask_WithoutModel_UsesTheDefault()
    {
        var chat = new FakeChatService();
        var catalog = new FakeModelCatalog();

        await RunAsync(chat, ["ask", "Hi"], catalog: catalog);

        Assert.Null(Assert.Single(chat.Calls).Model);
        Assert.Equal(0, catalog.Reads);
    }

    [Fact]
    public async Task Ask_UnknownModel_ExitsWithUsageErrorAndTheListBeforeReadingTheQuestion()
    {
        var chat = new FakeChatService();

        var run = await RunAsync(chat, ["ask", "--model", "gpt-image-2"], stdin: "never read\n");

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("'gpt-image-2' is not a chat model of the LiteLLM proxy. Available: gpt-5.6-luna, gpt-5.6-terra.", run.Error, StringComparison.Ordinal);
        Assert.Contains("Run 'dotnextai models' for the list.", run.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("Question:", run.Error, StringComparison.Ordinal);
        Assert.Empty(chat.Calls);
    }

    [Fact]
    public async Task Chat_ModelCommand_ShowsTheCurrentModelAndTheList()
    {
        var run = await RunAsync(new FakeChatService(), ["chat"], stdin: Lines("/model"), inputRedirected: true);

        Assert.Contains("Model: gpt-5.6-luna", run.Error, StringComparison.Ordinal);
        Assert.Contains("* gpt-5.6-luna", run.Error, StringComparison.Ordinal);
        Assert.Contains("  gpt-5.6-terra", run.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Chat_ModelCommand_SwitchesModelsAndTheConversationGoesOn()
    {
        var chat = new FakeChatService();

        var run = await RunAsync(chat, ["chat"], stdin: Lines("one", "/model gpt-5.6-terra", "two", "/model gpt-5.6-luna", "three"), inputRedirected: true);

        Assert.Contains("Now using gpt-5.6-terra. The conversation goes on with it.", run.Error, StringComparison.Ordinal);
        Assert.Equal([null, "gpt-5.6-terra", null], chat.Calls.Select(call => call.Model));
        Assert.Equal(4, chat.Calls[2].History.Length);
    }

    [Fact]
    public async Task Chat_ModelCommand_RejectsAModelThatIsNotForChat()
    {
        var chat = new FakeChatService();

        var run = await RunAsync(chat, ["chat"], stdin: Lines("/model text-embedding-3-small", "Hi"), inputRedirected: true);

        Assert.Contains("'text-embedding-3-small' is not a chat model", run.Error, StringComparison.Ordinal);
        Assert.Null(Assert.Single(chat.Calls).Model);
    }

    [Fact]
    public async Task Chat_ModelCommand_ReportsAListFailureAndTheChatGoesOn()
    {
        var catalog = new FakeModelCatalog { Failure = new ChatServiceException(ChatFailure.Unavailable, "Down.") };
        var chat = new FakeChatService();

        var run = await RunAsync(chat, ["chat"], stdin: Lines("/model gpt-5.6-terra", "Hi"), inputRedirected: true, catalog: catalog);

        Assert.Contains("Error: Down.", run.Error, StringComparison.Ordinal);
        Assert.Null(Assert.Single(chat.Calls).Model);
    }

    [Fact]
    public async Task Help_ListsTheModelsCommand()
    {
        var run = await RunAsync(new FakeChatService(), ["--help"]);

        Assert.Contains("models", run.Out, StringComparison.Ordinal);
    }
}
