using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

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
    }
}
