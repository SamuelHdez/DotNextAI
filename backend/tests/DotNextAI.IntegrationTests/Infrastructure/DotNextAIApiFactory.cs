using DotNextAI.Application.Chat;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DotNextAI.IntegrationTests.Infrastructure;

/// <summary>
/// In-memory API in the <c>Testing</c> environment: the developer's user-secrets (only loaded in Development) never
/// reach the tests, and no test calls the real LiteLLM proxy.
/// </summary>
public class DotNextAIApiFactory : WebApplicationFactory<Program>
{
    public const string FrontendOrigin = "http://localhost:3030";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Cors:AllowedOrigins:0", FrontendOrigin);
        // High enough for every test; the rate-limit tests lower it.
        builder.UseSetting("RateLimiting:ChatPermitsPerMinute", "10000");
    }

    /// <summary>A client of an API whose chat service is <paramref name="chat"/>.</summary>
    public HttpClient CreateClientWith(IChatService chat, int? chatPermitsPerMinute = null, IModelCatalog? catalog = null) =>
        WithWebHostBuilder(builder =>
        {
            if (chatPermitsPerMinute is { } permits)
            {
                builder.UseSetting(
                    "RateLimiting:ChatPermitsPerMinute",
                    permits.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            builder.ConfigureTestServices(services =>
            {
                services.Replace(ServiceDescriptor.Singleton(chat));
                services.Replace(ServiceDescriptor.Singleton(catalog ?? new Chat.FakeModelCatalog()));
            });
        }).CreateClient();
}
