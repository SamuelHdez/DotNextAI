using DotNextAI.Infrastructure.Chat;

namespace DotNextAI.UnitTests.Chat;

public class ModelPriceTests
{
    // gpt-6-luna in the ERNI proxy on 2026-11-17: $0.10 / $0.50 per million, cached input $0.01, above 272k $0.20 / $0.75.
    private static readonly ModelPrice Luna = new(
        0.0000001m,
        0.0000005m,
        0.00000001m,
        [new ModelPriceTier(272_000, 0.0000002m, 0.00000075m, 0.00000002m)]);

    [Fact]
    public void CostOf_InputAndOutput_AreEachAtTheirPrice()
    {
        Assert.Equal((1_200 * 0.0000001m) + (34 * 0.0000005m), Luna.CostOf(1_200, 0, 34));
    }

    [Fact]
    public void CostOf_CachedInput_IsAtTheCacheReadPrice()
    {
        // 1,000 input tokens, 800 of them cached: 200 × input + 800 × cache read.
        Assert.Equal((200 * 0.0000001m) + (800 * 0.00000001m), Luna.CostOf(1_000, 800, 0));
    }

    [Fact]
    public void CostOf_CachedInputWithoutACacheReadPrice_IsAtTheInputPrice()
    {
        var price = new ModelPrice(0.000001m, 0.000002m);

        Assert.Equal(price.CostOf(1_000, 0, 10), price.CostOf(1_000, 600, 10));
    }

    [Fact]
    public void CostOf_MoreCachedThanInput_NeverGoesNegative()
    {
        Assert.Equal(100 * 0.00000001m, Luna.CostOf(100, 500, 0));
    }

    [Fact]
    public void CostOf_InputAboveTheTier_UsesTheTierPricesForTheWholeCall()
    {
        Assert.Equal((272_000 * 0.0000001m) + (10 * 0.0000005m), Luna.CostOf(272_000, 0, 10));
        Assert.Equal((272_001 * 0.0000002m) + (10 * 0.00000075m), Luna.CostOf(272_001, 0, 10));
    }

    [Fact]
    public void CostOf_TierWithoutOutputOrCachePrices_KeepsTheBaseOnes()
    {
        var price = new ModelPrice(0.000001m, 0.000002m, 0.0000001m, [new ModelPriceTier(1_000, 0.000003m)]);

        Assert.Equal((500 * 0.000003m) + (1_000 * 0.0000001m) + (10 * 0.000002m), price.CostOf(1_500, 1_000, 10));
    }

    [Fact]
    public void CostOf_IsRoundedToTenDecimals()
    {
        var price = new ModelPrice(0.000000000033m, 0m);

        Assert.Equal(0.0000000001m, price.CostOf(3, 0, 0));
    }
}
