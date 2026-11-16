namespace DotNextAI.Application.Chat;

/// <summary>
/// One piece of a streamed answer (backlog step 07). Every update but the last carries a <see cref="Text"/> fragment;
/// the last one carries <see cref="Completed"/>, the whole answer with the model and whether it was cut.
/// </summary>
/// <param name="Text">New text since the previous update (empty in the final update).</param>
/// <param name="Completed">The whole answer, only in the final update.</param>
public sealed record ChatStreamUpdate(string Text, ChatAnswer? Completed = null);
