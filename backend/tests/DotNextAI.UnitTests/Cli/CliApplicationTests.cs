using DotNextAI.Application.Chat;
using DotNextAI.Cli;

namespace DotNextAI.UnitTests.Cli;

public class CliApplicationTests
{
    internal sealed record Run(int ExitCode, string Out, string Error);

    internal static async Task<Run> RunAsync(
        FakeChatService chat,
        string[] args,
        string stdin = "",
        bool inputRedirected = false,
        CancellationToken? cancellationToken = null,
        FakeModelCatalog? catalog = null)
    {
        using var input = new StringReader(stdin);
        using var output = new StringWriter();
        using var error = new StringWriter();
        var console = new CliConsole(input, output, error, inputRedirected);

        var exitCode = await CliApplication.RunAsync(
            args,
            chat,
            catalog ?? new FakeModelCatalog(),
            console,
            cancellationToken ?? TestContext.Current.CancellationToken);
        return new Run(exitCode, output.ToString(), error.ToString());
    }

    [Fact]
    public async Task Ask_WithQuotedQuestion_PrintsOnlyTheAnswerOnStdout()
    {
        var chat = new FakeChatService();

        var run = await RunAsync(chat, ["ask", "What is .NET?"]);

        Assert.Equal(0, run.ExitCode);
        Assert.Equal("answer to What is .NET?" + Environment.NewLine, run.Out);
        Assert.Empty(run.Error);
        Assert.Equal(["What is .NET?"], chat.Questions);
    }

    [Fact]
    public async Task Ask_WithUnquotedWords_JoinsThemIntoOneQuestion()
    {
        var chat = new FakeChatService();

        var run = await RunAsync(chat, ["ask", "What", "is", ".NET?"]);

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(["What is .NET?"], chat.Questions);
    }

    [Fact]
    public async Task Ask_WithoutQuestionAndRedirectedStdin_ReadsAllOfStdin()
    {
        var chat = new FakeChatService();

        var run = await RunAsync(chat, ["ask"], stdin: "Summarize this:\nline one\nline two\n", inputRedirected: true);

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(["Summarize this:\nline one\nline two"], chat.Questions);
        Assert.DoesNotContain("Question:", run.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ask_WithoutQuestionInATerminal_PromptsOnStderrAndReadsOneLine()
    {
        var chat = new FakeChatService();

        var run = await RunAsync(chat, ["ask"], stdin: "What is .NET?\nignored second line\n");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal("Question: ", run.Error);
        Assert.Equal(["What is .NET?"], chat.Questions);
        Assert.Equal("answer to What is .NET?" + Environment.NewLine, run.Out);
    }

    [Fact]
    public async Task Ask_IsStateless_SendsNoHistory()
    {
        var chat = new FakeChatService();

        await RunAsync(chat, ["ask", "What is .NET?"]);

        Assert.Empty(Assert.Single(chat.Calls).History);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n")]
    public async Task Ask_EmptyQuestion_ExitsWithUsageErrorWithoutCallingTheModel(string stdin)
    {
        var chat = new FakeChatService();

        var run = await RunAsync(chat, ["ask"], stdin: stdin, inputRedirected: true);

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("The question is empty.", run.Error, StringComparison.Ordinal);
        Assert.Empty(chat.Questions);
    }

    [Fact]
    public async Task Ask_TooLongQuestion_ExitsWithUsageError()
    {
        var chat = new FakeChatService();

        var run = await RunAsync(chat, ["ask"], stdin: new string('a', ChatRules.MaxQuestionLength + 1), inputRedirected: true);

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("too long", run.Error, StringComparison.Ordinal);
        Assert.Empty(chat.Questions);
    }

    [Fact]
    public async Task Ask_NotConfigured_ExitsWithNotConfiguredBeforeReadingTheQuestion()
    {
        var chat = new FakeChatService { IsConfigured = false };

        var run = await RunAsync(chat, ["ask"], stdin: "What is .NET?\n");

        Assert.Equal(3, run.ExitCode);
        Assert.Contains("./scripts/dev.sh secrets", run.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("Question:", run.Error, StringComparison.Ordinal);
        Assert.Empty(chat.Questions);
    }

    [Theory]
    [InlineData(ChatFailure.Unauthorized, "./scripts/dev.sh secrets")]
    [InlineData(ChatFailure.ModelNotFound, "Llm:DefaultModel")]
    [InlineData(ChatFailure.ModelUnavailable, "Try again later")]
    [InlineData(ChatFailure.Timeout, "Check the network")]
    public async Task Ask_ModelFailure_PrintsTheErrorAndAHintAndExitsWithModelError(ChatFailure failure, string hint)
    {
        var chat = FakeChatService.Failing(failure, "Something failed.");

        var run = await RunAsync(chat, ["ask", "ping"]);

        Assert.Equal(1, run.ExitCode);
        Assert.Empty(run.Out);
        Assert.Contains("Error: Something failed.", run.Error, StringComparison.Ordinal);
        Assert.Contains(hint, run.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ask_StreamedAnswer_IsWrittenPieceByPieceAsOneLine()
    {
        var chat = new FakeChatService(_ => new ChatAnswer("one two three", "gpt-5.6-luna", Truncated: false));

        var run = await RunAsync(chat, ["ask", "Count"]);

        Assert.Equal(0, run.ExitCode);
        Assert.Equal("one two three" + Environment.NewLine, run.Out);
    }

    [Fact]
    public async Task Ask_AnswerBreaksMidStream_EndsThePartialLineAndReportsTheError()
    {
        var chat = new FakeChatService(_ => new ChatAnswer("one two three", "gpt-5.6-luna", Truncated: false))
        {
            FailMidStream = new ChatServiceException(ChatFailure.Unavailable, "The LiteLLM proxy could not be reached."),
        };

        var run = await RunAsync(chat, ["ask", "Count"]);

        Assert.Equal(1, run.ExitCode);
        Assert.Equal("one " + Environment.NewLine, run.Out);
        Assert.Contains("Error: The LiteLLM proxy could not be reached.", run.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ask_TruncatedAnswer_PrintsTheAnswerAndAWarningOnStderr()
    {
        var chat = new FakeChatService(_ => new ChatAnswer("partial", "gpt-5.6-luna", Truncated: true));

        var run = await RunAsync(chat, ["ask", "Tell me everything"]);

        Assert.Equal(0, run.ExitCode);
        Assert.Equal("partial" + Environment.NewLine, run.Out);
        Assert.Contains("cut off", run.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ask_Cancelled_ExitsWith130()
    {
        var chat = new FakeChatService();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var run = await RunAsync(chat, ["ask", "ping"], cancellationToken: cancelled.Token);

        Assert.Equal(130, run.ExitCode);
        Assert.Empty(chat.Questions);
    }

    [Fact]
    public async Task Help_ListsTheChatAndAskCommands()
    {
        var run = await RunAsync(new FakeChatService(), ["--help"]);

        Assert.Equal(0, run.ExitCode);
        Assert.Contains("chat", run.Out, StringComparison.Ordinal);
        Assert.Contains("ask", run.Out, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--no-such-option")]
    [InlineData("no-such-command")]
    public async Task UnknownCommandOrOption_ExitsWithUsageErrorAndPointsToHelp(string arg)
    {
        var chat = new FakeChatService();

        var run = await RunAsync(chat, [arg]);

        Assert.Equal(2, run.ExitCode);
        Assert.Contains(arg, run.Error, StringComparison.Ordinal);
        Assert.Contains("dotnextai --help", run.Error, StringComparison.Ordinal);
        Assert.Empty(chat.Questions);
    }
}
