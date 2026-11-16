using DotNextAI.Application.Conversations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DotNextAI.Persistence;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Chats";

    /// <summary>The default database file, relative to the API's content root.</summary>
    public const string DefaultDataSource = "App_Data/dotnextai.db";

    /// <summary>
    /// Registers the saved chats database (SQLite) and the conversation service. A relative <c>Data Source</c> is
    /// resolved against <paramref name="contentRoot"/>, so the file does not depend on the working directory.
    /// </summary>
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration, string contentRoot)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var connection = new SqliteConnectionStringBuilder(
            configuration.GetConnectionString(ConnectionStringName) ?? $"Data Source={DefaultDataSource}");
        if (connection.DataSource != ":memory:" && !Path.IsPathRooted(connection.DataSource))
        {
            connection.DataSource = Path.GetFullPath(Path.Combine(contentRoot, connection.DataSource));
        }

        if (Path.GetDirectoryName(connection.DataSource) is { Length: > 0 } folder)
        {
            Directory.CreateDirectory(folder);
        }

        services.AddDbContext<ChatsDbContext>(options => options.UseSqlite(connection.ToString()));
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IConversationStore, SqliteConversationStore>();
        services.AddScoped<ConversationService>();
        return services;
    }

    /// <summary>Applies pending migrations (startup of the local app; <c>Database:MigrateOnStartup</c>).</summary>
    public static async Task MigrateChatsDatabaseAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ChatsDbContext>().Database.MigrateAsync();
    }
}
