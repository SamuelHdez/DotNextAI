using DotNextAI.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace DotNextAI.Persistence;

/// <summary>The saved chats database (SQLite file, single local user). Migrations live in Migrations/.</summary>
internal sealed class ChatsDbContext(DbContextOptions<ChatsDbContext> options) : DbContext(options)
{
    public DbSet<FolderEntity> Folders => Set<FolderEntity>();

    public DbSet<ConversationEntity> Conversations => Set<ConversationEntity>();

    public DbSet<MessageEntity> Messages => Set<MessageEntity>();

    public DbSet<MessageImageEntity> MessageImages => Set<MessageImageEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FolderEntity>(folder =>
        {
            folder.ToTable("Folders");
            folder.Property(f => f.Name).HasMaxLength(100).UseCollation("NOCASE");
            // "Work" and "work" are the same folder.
            folder.HasIndex(f => f.Name).IsUnique();
        });

        modelBuilder.Entity<ConversationEntity>(conversation =>
        {
            conversation.ToTable("Conversations");
            conversation.Property(c => c.Title).HasMaxLength(200);
            conversation.Property(c => c.Model).HasMaxLength(200);
            // Deleting a folder keeps its conversations, without a folder.
            conversation.HasOne(c => c.Folder).WithMany(f => f.Conversations).HasForeignKey(c => c.FolderId).OnDelete(DeleteBehavior.SetNull);
            conversation.HasIndex(c => c.UpdatedAt);
        });

        modelBuilder.Entity<MessageEntity>(message =>
        {
            message.ToTable("Messages");
            message.Property(m => m.Model).HasMaxLength(200);
            message.HasOne(m => m.Conversation).WithMany(c => c.Messages).HasForeignKey(m => m.ConversationId).OnDelete(DeleteBehavior.Cascade);
            message.HasIndex(m => new { m.ConversationId, m.Sequence }).IsUnique();
        });

        modelBuilder.Entity<MessageImageEntity>(image =>
        {
            image.ToTable("MessageImages");
            image.Property(i => i.MediaType).HasMaxLength(50);
            image.HasOne(i => i.Message).WithMany(m => m.Images).HasForeignKey(i => i.MessageId).OnDelete(DeleteBehavior.Cascade);
            image.HasIndex(i => new { i.MessageId, i.Position }).IsUnique();
        });
    }
}
