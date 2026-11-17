using System.Net;
using DotNextAI.Infrastructure.Chat;
using DotNextAI.Infrastructure.Options;
using DotNextAI.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNextAI.UnitTests.Chat;

public class LiteLlmModelPricesTests
{
    private static readonly string ApiKey = $"sk-{Guid.NewGuid():N}";

    // GET /model/info of the ERNI LiteLLM proxy, 2026-11-17 (trimmed to the fields that matter).
    private const string ModelInfo = """
        {"data":[
          {"model_name":"gpt-image-2","litellm_params":{"model":"azure/gpt-image-2"},"model_info":{"mode":"image_generation","input_cost_per_token":5e-06,"output_cost_per_token":0,"output_cost_per_image_token":3e-05}},
          {"model_name":"gpt-6-luna","litellm_params":{"model":"azure/gpt-6-luna"},"model_info":{"mode":"chat","input_cost_per_token":1.0000000000000001e-07,"output_cost_per_token":5e-07,"cache_read_input_token_cost":1e-08,"cache_creation_input_token_cost":1.2500000000000001e-08,"input_cost_per_token_above_272k_tokens":2e-07,"output_cost_per_token_above_272k_tokens":7.5e-07,"cache_read_input_token_cost_above_272k_tokens":2e-08,"search_context_cost_per_query":{"search_context_size_low":0.014}}},
          {"model_name":"gpt-6.1-sol","litellm_params":{"model":"azure/gpt-6.1-sol"},"model_info":{"mode":"chat","input_cost_per_token":2e-06,"output_cost_per_token":1.1e-05,"cache_read_input_token_cost":1.1e-07,"input_cost_per_token_flex":1e-06,"output_cost_per_token_flex":5e-06,"input_cost_per_token_above_272k_tokens":4e-06,"input_cost_per_token_above_272k_tokens_flex":2e-06,"output_cost_per_token_above_272k_tokens":1.5e-05}},
          {"model_name":"gpt-6.1-sol","litellm_params":{"model":"azure/gpt-6.1-sol-eu"},"model_info":{"mode":"chat","input_cost_per_token":9e-06,"output_cost_per_token":9e-05}},
          {"model_name":"local-model","litellm_params":{"model":"ollama/llama"},"model_info":{"mode":"chat","input_cost_per_token":0,"output_cost_per_token":0}},
          {"model_name":"no-info","litellm_params":{"model":"x"}}
        ]}
        """;

    private static LiteLlmModelPrices CreatePrices(StubHttpHandler handler, TimeProvider? time = null, string endpoint = "https://litellm.example.com/") =>
        new(
            Microsoft.Extensions.Options.Options.Create(new LlmOptions { Endpoint = endpoint, ApiKey = ApiKey, DefaultModel = "gpt-6-luna", TimeoutSeconds = 30 }),
            new StubHttpClientFactory(handler),
            time ?? TimeProvider.System,
            NullLogger<LiteLlmModelPrices>.Instance);

    [Fact]
    public async Task FindAsync_ProxyPrices_AreReadPerTokenWithTheirTier()
    {
        using var prices = CreatePrices(StubHttpHandler.Json(HttpStatusCode.OK, ModelInfo));

        var luna = await prices.FindAsync("gpt-6-luna", TestContext.Current.CancellationToken);

        Assert.NotNull(luna);
        Assert.Equal(0.0000001m, luna.InputPerToken);
        Assert.Equal(0.0000005m, luna.OutputPerToken);
        Assert.Equal(0.00000001m, luna.CachedInputPerToken);
        var tier = Assert.Single(luna.Tiers!);
        Assert.Equal(new ModelPriceTier(272_000, 0.0000002m, 0.00000075m, 0.00000002m), tier);
    }

    [Fact]
    public async Task FindAsync_FlexPricesAndLaterDeployments_AreIgnored()
    {
        using var prices = CreatePrices(StubHttpHandler.Json(HttpStatusCode.OK, ModelInfo));

        var sol = await prices.FindAsync("gpt-6.1-sol", TestContext.Current.CancellationToken);

        Assert.NotNull(sol);
        Assert.Equal(0.000002m, sol.InputPerToken);
        Assert.Equal(0.000011m, sol.OutputPerToken);
        Assert.Equal(new ModelPriceTier(272_000, 0.000004m, 0.000015m, null), Assert.Single(sol.Tiers!));
    }

    [Theory]
    [InlineData("local-model")]
    [InlineData("no-info")]
    [InlineData("unknown-model")]
    public async Task FindAsync_ModelWithoutPrices_IsNull(string model)
    {
        using var prices = CreatePrices(StubHttpHandler.Json(HttpStatusCode.OK, ModelInfo));

        Assert.Null(await prices.FindAsync(model, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task FindAsync_CallsTheModelInfoRouteOfTheEndpointWithTheKey()
    {
        var handler = StubHttpHandler.Json(HttpStatusCode.OK, ModelInfo);
        using var prices = CreatePrices(handler, endpoint: "https://litellm.example.com/v1/");

        await prices.FindAsync("gpt-6-luna", TestContext.Current.CancellationToken);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(new Uri("https://litellm.example.com/v1/model/info"), request.Uri);
        Assert.Equal($"Bearer {ApiKey}", request.Authorization);
    }

    [Fact]
    public async Task FindAsync_IsCachedForFiveMinutes()
    {
        var handler = StubHttpHandler.Json(HttpStatusCode.OK, ModelInfo);
        var time = new ManualTimeProvider();
        using var prices = CreatePrices(handler, time);

        await prices.FindAsync("gpt-6-luna", TestContext.Current.CancellationToken);
        await prices.FindAsync("gpt-6.1-sol", TestContext.Current.CancellationToken);
        time.Advance(LiteLlmModelPrices.CacheDuration);
        await prices.FindAsync("gpt-6-luna", TestContext.Current.CancellationToken);

        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task FindAsync_ProxyFailure_GivesNoPriceAndRetriesAfterAMinute()
    {
        var fail = true;
        var handler = new StubHttpHandler(_ => fail
            ? new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("""{"error":{"message":"Not Found"}}""") }
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ModelInfo) });
        var time = new ManualTimeProvider();
        using var prices = CreatePrices(handler, time);

        Assert.Null(await prices.FindAsync("gpt-6-luna", TestContext.Current.CancellationToken));
        fail = false;
        Assert.Null(await prices.FindAsync("gpt-6-luna", TestContext.Current.CancellationToken));
        time.Advance(LiteLlmModelPrices.RetryAfterFailure);

        Assert.NotNull(await prices.FindAsync("gpt-6-luna", TestContext.Current.CancellationToken));
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task FindAsync_InvalidJson_GivesNoPrice()
    {
        using var prices = CreatePrices(StubHttpHandler.Json(HttpStatusCode.OK, "<html>"));

        Assert.Null(await prices.FindAsync("gpt-6-luna", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task FindAsync_NotConfigured_GivesNoPriceWithoutCallingTheProxy()
    {
        var handler = StubHttpHandler.Json(HttpStatusCode.OK, ModelInfo);
        using var prices = new LiteLlmModelPrices(
            Microsoft.Extensions.Options.Options.Create(new LlmOptions()),
            new StubHttpClientFactory(handler),
            TimeProvider.System,
            NullLogger<LiteLlmModelPrices>.Instance);

        Assert.Null(await prices.FindAsync("gpt-6-luna", TestContext.Current.CancellationToken));
        Assert.Empty(handler.Requests);
    }
}
