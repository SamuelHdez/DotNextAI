namespace DotNextAI.Application.Chat;

/// <summary>Who wrote a turn of the conversation.</summary>
public enum ChatTurnRole
{
    User = 0,
    Assistant,
}

/// <summary>One message of a conversation's history, as kept by the front end (CLI process, browser).</summary>
public sealed record ChatTurn(ChatTurnRole Role, string Text);
