using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DotNextAI.Persistence;

/// <summary>Lets `dotnet ef` build the context without starting the API (migrations, model checks).</summary>
internal sealed class DesignTimeChatsDbContextFactory : IDesignTimeDbContextFactory<ChatsDbContext>
{
    public ChatsDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<ChatsDbContext>().UseSqlite("Data Source=design-time.db").Options);
}
