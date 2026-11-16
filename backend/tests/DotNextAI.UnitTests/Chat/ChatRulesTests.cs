using DotNextAI.Application.Chat;

namespace DotNextAI.UnitTests.Chat;

public class ChatRulesTests
{
    [Fact]
    public void TryNormalizeQuestion_TrimsWhitespace()
    {
        Assert.True(ChatRules.TryNormalizeQuestion("  hello \n", out var normalized, out var error));
        Assert.Equal("hello", normalized);
        Assert.Null(error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\n ")]
    public void TryNormalizeQuestion_Empty_IsRejected(string? question)
    {
        Assert.False(ChatRules.TryNormalizeQuestion(question, out var normalized, out var error));
        Assert.Empty(normalized);
        Assert.Equal("The question is empty.", error);
    }

    [Fact]
    public void TryNormalizeQuestion_AtTheLimit_IsAccepted()
    {
        Assert.True(ChatRules.TryNormalizeQuestion(new string('a', ChatRules.MaxQuestionLength), out _, out _));
    }

    [Fact]
    public void TryNormalizeQuestion_OverTheLimit_IsRejected()
    {
        Assert.False(ChatRules.TryNormalizeQuestion(new string('a', ChatRules.MaxQuestionLength + 1), out _, out var error));
        Assert.StartsWith("The question is too long", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateHistory_AtTheLimit_IsAccepted()
    {
        var history = Enumerable.Range(0, ChatRules.MaxHistoryTurns)
            .Select(i => new ChatTurn(i % 2 == 0 ? ChatTurnRole.User : ChatTurnRole.Assistant, $"turn {i}"))
            .ToArray();

        ChatRules.ValidateHistory(history);
    }

    [Fact]
    public void ValidateHistory_OverTheLimit_IsRejected()
    {
        var history = Enumerable.Range(0, ChatRules.MaxHistoryTurns + 1)
            .Select(i => new ChatTurn(ChatTurnRole.User, $"turn {i}"))
            .ToArray();

        var ex = Assert.Throws<ArgumentException>(() => ChatRules.ValidateHistory(history));
        Assert.StartsWith("The conversation is too long", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateHistory_EmptyTurn_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => ChatRules.ValidateHistory([new ChatTurn(ChatTurnRole.Assistant, " ")]));
    }

    [Fact]
    public void NormalizeQuestion_Invalid_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ChatRules.NormalizeQuestion(" "));
    }
}
