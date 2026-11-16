using DotNextAI.Infrastructure.Options;

namespace DotNextAI.UnitTests.Options;

public class LlmOptionsTests
{
    // Generated per run: Semgrep (p/secrets) blocks literal keys.
    private static readonly string ApiKey = $"sk-{Guid.NewGuid():N}";

    [Theory]
    [InlineData("https://litellm.example.com/")]
    [InlineData("http://localhost:4000/")]
    [InlineData("http://127.0.0.1:4000/")]
    public void IsConfigured_AllowedEndpointKeyAndModel_ReturnsTrue(string endpoint)
    {
        var options = new LlmOptions { Endpoint = endpoint, ApiKey = ApiKey, DefaultModel = "gpt-4o-mini" };

        Assert.True(options.IsConfigured);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-url")]
    [InlineData("http://litellm.example.com/")]
    [InlineData("ftp://litellm.example.com/")]
    public void IsConfigured_MissingOrUnsafeEndpoint_ReturnsFalse(string? endpoint)
    {
        var options = new LlmOptions { Endpoint = endpoint, ApiKey = ApiKey, DefaultModel = "gpt-4o-mini" };

        Assert.False(options.IsConfigured);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsConfigured_MissingApiKey_ReturnsFalse(string? apiKey)
    {
        var options = new LlmOptions { Endpoint = "https://litellm.example.com/", ApiKey = apiKey, DefaultModel = "gpt-4o-mini" };

        Assert.False(options.IsConfigured);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void IsConfigured_MissingDefaultModel_ReturnsFalse(string? model)
    {
        var options = new LlmOptions { Endpoint = "https://litellm.example.com/", ApiKey = ApiKey, DefaultModel = model };

        Assert.False(options.IsConfigured);
    }
}
