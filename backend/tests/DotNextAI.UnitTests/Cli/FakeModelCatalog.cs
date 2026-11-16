using DotNextAI.Application.Chat;

namespace DotNextAI.UnitTests.Cli;

/// <summary>Model catalog double: the default "gpt-5.6-luna" and "gpt-5.6-terra", or a failure.</summary>
internal sealed class FakeModelCatalog : IModelCatalog
{
    public string? DefaultModel { get; init; } = "gpt-5.6-luna";

    public ChatServiceException? Failure { get; init; }

    public IReadOnlyList<ChatModel> Models { get; init; } =
    [
        new("gpt-5.6-luna", IsDefault: true, MaxInputTokens: 922_000, MaxOutputTokens: 128_000),
        new("gpt-5.6-terra", IsDefault: false, MaxInputTokens: 922_000, MaxOutputTokens: 128_000),
    ];

    public int Reads { get; private set; }

    public Task<IReadOnlyList<ChatModel>> GetChatModelsAsync(CancellationToken cancellationToken)
    {
        Reads++;
        return Failure is null ? Task.FromResult(Models) : Task.FromException<IReadOnlyList<ChatModel>>(Failure);
    }
}
