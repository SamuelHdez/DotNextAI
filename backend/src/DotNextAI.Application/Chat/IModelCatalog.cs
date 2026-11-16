namespace DotNextAI.Application.Chat;

/// <summary>A chat model offered by the LiteLLM proxy (backlog step 08).</summary>
/// <param name="Id">Model name to send to the proxy, e.g. <c>gpt-5.6-terra</c>.</param>
/// <param name="IsDefault">Whether it is <c>Llm:DefaultModel</c>.</param>
/// <param name="MaxInputTokens">Context window reported by the proxy, when known.</param>
/// <param name="MaxOutputTokens">Longest answer reported by the proxy, when known.</param>
public sealed record ChatModel(string Id, bool IsDefault, int? MaxInputTokens = null, int? MaxOutputTokens = null);

/// <summary>The chat models of the proxy, used by the model selector and to accept only known models.</summary>
public interface IModelCatalog
{
    /// <summary>The configured default model (<c>Llm:DefaultModel</c>), or null when the proxy is not configured.</summary>
    string? DefaultModel { get; }

    /// <summary>
    /// Chat models of the proxy (<c>mode = chat</c>), the default first. Cached for a few minutes; the default model is
    /// always included, even if the proxy does not list it.
    /// </summary>
    /// <exception cref="ChatServiceException">The list could not be read; <see cref="ChatServiceException.Failure"/> says why.</exception>
    Task<IReadOnlyList<ChatModel>> GetChatModelsAsync(CancellationToken cancellationToken);
}
