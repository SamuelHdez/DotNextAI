using DotNextAI.Infrastructure.Chat;

namespace DotNextAI.UnitTests.TestSupport;

/// <summary>Fixed prices per model; records which models were priced.</summary>
internal sealed class FakeModelPrices(IReadOnlyDictionary<string, ModelPrice> prices) : IModelPrices
{
    public List<string> Asked { get; } = [];

    public Task<ModelPrice?> FindAsync(string model, CancellationToken cancellationToken)
    {
        Asked.Add(model);
        return Task.FromResult(prices.GetValueOrDefault(model));
    }
}
