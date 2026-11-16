using DotNextAI.Application.Chat;
using DotNextAI.Cli.Commands;
using static DotNextAI.UnitTests.Cli.CliApplicationTests;

namespace DotNextAI.UnitTests.Cli;

public class ChatCommandTests
{
    private static string Lines(params string[] lines) => string.Join("\n", lines) + "\n";

    [Fact]
    public async Task NoCommand_StartsTheChat()
    {
        var chat = new FakeChatService();

        var run = await RunAsync(chat, [], stdin: Lines("Hi"), inputRedirected: true);

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(["Hi"], chat.Questions);
    }

    [Fact]
    public async Task Chat_SendsThePreviousTurnsWithEveryQuestion()
    {
        var chat = new FakeChatService();

        var run = await RunAsync(chat, ["chat"], stdin: Lines("My name is Ana.", "What is my name?"), inputRedirected: true);

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(2, chat.Calls.Count);
        Assert.Empty(chat.Calls[0].History);
        Assert.Equal(
            [
                new ChatTurn(ChatTurnRole.User, "My name is Ana."),
                new ChatTurn(ChatTurnRole.Assistant, "answer to My name is Ana."),
            ],
            chat.Calls[1].History);
    }

    [Fact]
    public async Task Chat_WithRedirectedStdin_WritesOnlyTheAnswers()
    {
        var chat = new FakeChatService();

        var run = await RunAsync(chat, ["chat"], stdin: Lines("one", "two"), inputRedirected: true);

        Assert.Equal($"answer to one{Environment.NewLine}answer to two{Environment.NewLine}", run.Out);
        Assert.Empty(run.Error);
    }

    [Fact]
    public async Task Chat_InATerminal_ShowsTheBannerAndPromptsOnStderr()
    {
        var chat = new FakeChatService();

        var run = await RunAsync(chat, ["chat"], stdin: Lines("Hi"));

        Assert.Equal(0, run.ExitCode);
        Assert.StartsWith(ChatCommand.Banner, run.Error, StringComparison.Ordinal);
        Assert.Contains("You: ", run.Error, StringComparison.Ordinal);
        Assert.Equal("answer to Hi" + Environment.NewLine, run.Out);
    }

    [Theory]
    [InlineData("/exit")]
    [InlineData("/quit")]
    [InlineData("/EXIT")]
    public async Task Chat_ExitCommand_EndsTheChat(string command)
    {
        var chat = new FakeChatService();

        var run = await RunAsync(chat, ["chat"], stdin: Lines("one", command, "never asked"), inputRedirected: true);

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(["one"], chat.Questions);
    }

    [Fact]
    public async Task Chat_EndOfInput_EndsTheChatWithSuccess()
    {
        var run = await RunAsync(new FakeChatService(), ["chat"], stdin: string.Empty);

        Assert.Equal(0, run.ExitCode);
    }

    [Fact]
    public async Task Chat_NewCommand_ForgetsThePreviousTurns()
    {
        var chat = new FakeChatService();

        var run = await RunAsync(chat, ["chat"], stdin: Lines("one", "/new", "two"), inputRedirected: true);

        Assert.Equal(2, chat.Calls.Count);
        Assert.Empty(chat.Calls[1].History);
        Assert.Contains("Started a new conversation.", run.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Chat_HelpCommand_ListsTheChatCommands()
    {
        var chat = new FakeChatService();

        var run = await RunAsync(chat, ["chat"], stdin: Lines("/help"), inputRedirected: true);

        Assert.Contains("/new", run.Error, StringComparison.Ordinal);
        Assert.Contains("/exit", run.Error, StringComparison.Ordinal);
        Assert.Empty(chat.Calls);
    }

    [Fact]
    public async Task Chat_UnknownCommand_IsReportedAndNotSentToTheModel()
    {
        var chat = new FakeChatService();

        var run = await RunAsync(chat, ["chat"], stdin: Lines("/nope", "one"), inputRedirected: true);

        Assert.Contains("Unknown command '/nope'", run.Error, StringComparison.Ordinal);
        Assert.Equal(["one"], chat.Questions);
    }

    [Fact]
    public async Task Chat_EmptyLines_AreIgnored()
    {
        var chat = new FakeChatService();

        var run = await RunAsync(chat, ["chat"], stdin: Lines("", "   ", "one"), inputRedirected: true);

        Assert.Equal(["one"], chat.Questions);
        Assert.Empty(run.Error);
    }

    [Fact]
    public async Task Chat_FailedQuestion_IsReportedAndNotRecorded()
    {
        var calls = 0;
        var chat = new FakeChatService(q => ++calls == 1
            ? throw new ChatServiceException(ChatFailure.ModelUnavailable, "The model is down.")
            : new ChatAnswer($"answer to {q}", "gpt-5.6-luna", Truncated: false));

        var run = await RunAsync(chat, ["chat"], stdin: Lines("first", "second"), inputRedirected: true);

        Assert.Equal(0, run.ExitCode);
        Assert.Contains("Error: The model is down.", run.Error, StringComparison.Ordinal);
        Assert.Contains("Try again later", run.Error, StringComparison.Ordinal);
        Assert.Empty(chat.Calls[1].History);
        Assert.Equal("answer to second" + Environment.NewLine, run.Out);
    }

    [Fact]
    public async Task Chat_TooLongQuestion_IsRejectedAndTheChatContinues()
    {
        var chat = new FakeChatService();

        var run = await RunAsync(
            chat,
            ["chat"],
            stdin: Lines(new string('a', ChatRules.MaxQuestionLength + 1), "short"),
            inputRedirected: true);

        Assert.Contains("too long", run.Error, StringComparison.Ordinal);
        Assert.Equal(["short"], chat.Questions);
    }

    [Fact]
    public async Task Chat_AtTheQuestionLimit_AsksForANewConversation()
    {
        var chat = new FakeChatService();
        var questions = Enumerable.Range(1, ChatRules.MaxConversationQuestions + 1).Select(i => $"q{i}").ToList();
        questions.AddRange(["/new", "fresh"]);

        var run = await RunAsync(chat, ["chat"], stdin: Lines([.. questions]), inputRedirected: true);

        Assert.Equal(ChatRules.MaxConversationQuestions + 1, chat.Calls.Count);
        Assert.DoesNotContain($"q{ChatRules.MaxConversationQuestions + 1}", chat.Questions);
        Assert.Contains("reached the limit of 50 questions", run.Error, StringComparison.Ordinal);
        Assert.Equal("fresh", chat.Calls[^1].Question);
        Assert.Empty(chat.Calls[^1].History);
    }

    [Fact]
    public async Task Chat_NotConfigured_ExitsWithNotConfigured()
    {
        var chat = new FakeChatService { IsConfigured = false };

        var run = await RunAsync(chat, ["chat"], stdin: Lines("one"));

        Assert.Equal(3, run.ExitCode);
        Assert.DoesNotContain(ChatCommand.Banner, run.Error, StringComparison.Ordinal);
        Assert.Empty(chat.Calls);
    }

    [Fact]
    public async Task Chat_Cancelled_ExitsWith130()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var run = await RunAsync(new FakeChatService(), ["chat"], stdin: Lines("one"), cancellationToken: cancelled.Token);

        Assert.Equal(130, run.ExitCode);
    }
}
