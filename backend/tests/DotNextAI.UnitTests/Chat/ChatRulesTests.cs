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
    public void NormalizeQuestion_Invalid_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ChatRules.NormalizeQuestion(" "));
    }
}
