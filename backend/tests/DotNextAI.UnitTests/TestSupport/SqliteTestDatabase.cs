using DotNextAI.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DotNextAI.UnitTests.TestSupport;

/// <summary>An in-memory SQLite database with the real migrations applied, alive while this object is.</summary>
internal sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public SqliteTestDatabase()
    {
        _connection.Open();
        using var context = NewContext();
        context.Database.Migrate();
    }

    public ManualTimeProvider Time { get; } = new();

    public ChatsDbContext NewContext() => new(new DbContextOptionsBuilder<ChatsDbContext>().UseSqlite(_connection).Options);

    /// <summary>A store over a new context, as each request gets in the API.</summary>
    public SqliteConversationStore NewStore() => new(NewContext(), Time);

    public void Dispose() => _connection.Dispose();
}
