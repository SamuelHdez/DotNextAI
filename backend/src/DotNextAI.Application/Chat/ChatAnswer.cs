namespace DotNextAI.Application.Chat;

/// <summary>Answer of the model to one question.</summary>
/// <param name="Text">Answer text (or the model's refusal message).</param>
/// <param name="Model">Model that answered, as reported by the proxy.</param>
/// <param name="Truncated">True when the answer stopped because it reached the maximum output length.</param>
/// <param name="Usage">Tokens of this call (step 10); null when the proxy does not report them.</param>
public sealed record ChatAnswer(string Text, string Model, bool Truncated, TokenUsage? Usage = null);
