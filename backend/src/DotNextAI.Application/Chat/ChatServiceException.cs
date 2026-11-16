namespace DotNextAI.Application.Chat;

/// <summary>
/// A model call failed. The message is meant for the user who made the call: it may quote the first line of the
/// proxy's error message (400/404/422 only), and never contains the API key. Do not log it above Debug.
/// </summary>
public sealed class ChatServiceException : Exception
{
    public ChatServiceException()
    {
    }

    public ChatServiceException(string message)
        : base(message)
    {
    }

    public ChatServiceException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }

    public ChatServiceException(ChatFailure failure, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Failure = failure;
    }

    public ChatFailure Failure { get; } = ChatFailure.Unavailable;
}
