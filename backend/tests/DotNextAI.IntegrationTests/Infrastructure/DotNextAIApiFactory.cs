using DotNextAI.Application.Chat;
using DotNextAI.Application.Images;
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

    private readonly List<string> _databases = [];

    /// <summary>A fresh SQLite file for the saved chats, deleted with the factory.</summary>
    private string NewDatabase()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dotnextai-tests-{Guid.NewGuid():N}.db");
        _databases.Add(path);
        return $"Data Source={path};Pooling=False";
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        foreach (var database in _databases)
        {
            File.Delete(database);
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Chats", NewDatabase());
        builder.UseSetting("Cors:AllowedOrigins:0", FrontendOrigin);
        // High enough for every test; the rate-limit tests lower it.
        builder.UseSetting("RateLimiting:ChatPermitsPerMinute", "10000");
        builder.UseSetting("RateLimiting:ImagePermitsPerMinute", "10000");
    }

    /// <summary>A client of an API whose chat service is <paramref name="chat"/>.</summary>
    public HttpClient CreateClientWith(
        IChatService chat,
        int? chatPermitsPerMinute = null,
        IModelCatalog? catalog = null,
        IImageService? images = null,
        int? imagePermitsPerMinute = null) =>
        WithWebHostBuilder(builder =>
        {
            // Every client gets its own database, so tests never see each other's chats.
            builder.UseSetting("ConnectionStrings:Chats", NewDatabase());
            if (chatPermitsPerMinute is { } permits)
            {
                builder.UseSetting(
                    "RateLimiting:ChatPermitsPerMinute",
                    permits.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            if (imagePermitsPerMinute is { } imagePermits)
            {
                builder.UseSetting(
                    "RateLimiting:ImagePermitsPerMinute",
                    imagePermits.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            builder.ConfigureTestServices(services =>
            {
                services.Replace(ServiceDescriptor.Singleton(chat));
                services.Replace(ServiceDescriptor.Singleton(images ?? new Chat.FakeImageService()));
                services.Replace(ServiceDescriptor.Singleton(catalog ?? new Chat.FakeModelCatalog()));
            });
        }).CreateClient();
}
