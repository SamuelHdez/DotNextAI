using System.Net;
using System.Net.Http.Json;
using DotNextAI.Application.Health;
using DotNextAI.IntegrationTests.Infrastructure;

namespace DotNextAI.IntegrationTests.Health;

public class HealthEndpointTests(DotNextAIApiFactory factory) : IClassFixture<DotNextAIApiFactory>
{
    [Fact]
    public async Task GetHealth_WithoutLlmSettings_ReturnsHealthyAndNotConfigured()
    {
        using var client = factory.CreateClient();

        var health = await client.GetFromJsonAsync<HealthStatusDto>("/api/health", TestContext.Current.CancellationToken);

        Assert.NotNull(health);
        Assert.Equal("Healthy", health.Status);
        Assert.Equal("DotNextAI.Api", health.Service);
        Assert.Equal("Testing", health.Environment);
        Assert.False(health.LlmConfigured);
    }

    [Fact]
    public async Task GetHealth_WithLlmSettings_ReportsConfigured()
    {
        using var configured = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Llm:Endpoint", "https://litellm.example.com/");
            builder.UseSetting("Llm:ApiKey", $"sk-{Guid.NewGuid():N}");
            builder.UseSetting("Llm:DefaultModel", "gpt-4o-mini");
        });
        using var client = configured.CreateClient();

        var health = await client.GetFromJsonAsync<HealthStatusDto>("/api/health", TestContext.Current.CancellationToken);

        Assert.NotNull(health);
        Assert.True(health.LlmConfigured);
    }

    [Fact]
    public async Task GetHealth_NeverExposesLlmSecrets()
    {
        var apiKey = $"sk-{Guid.NewGuid():N}";
        using var configured = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Llm:Endpoint", "https://litellm.example.com/");
            builder.UseSetting("Llm:ApiKey", apiKey);
            builder.UseSetting("Llm:DefaultModel", "gpt-4o-mini");
        });
        using var client = configured.CreateClient();

        var body = await client.GetStringAsync(new Uri("/api/health", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.DoesNotContain(apiKey, body, StringComparison.Ordinal);
        Assert.DoesNotContain("litellm.example.com", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Preflight_FromFrontendOrigin_IsAllowed()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, new Uri("/api/health", UriKind.Relative));
        request.Headers.Add("Origin", DotNextAIApiFactory.FrontendOrigin);
        request.Headers.Add("Access-Control-Request-Method", "GET");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(DotNextAIApiFactory.FrontendOrigin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    [Fact]
    public async Task Preflight_FromUnknownOrigin_IsNotAllowed()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, new Uri("/api/health", UriKind.Relative));
        request.Headers.Add("Origin", "https://evil.example.com");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
