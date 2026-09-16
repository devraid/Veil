using Microsoft.EntityFrameworkCore;

namespace Veil.Data;

public sealed class VeilDbContext : DbContext
{
    public VeilDbContext(DbContextOptions<VeilDbContext> options)
        : base(options)
    {
    }

    public DbSet<Chat> Chats => Set<Chat>();

    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Chat>(entity =>
        {
            entity.ToTable("Chat");
            entity.HasKey(chat => chat.Id);
            entity.Property(chat => chat.Title).HasMaxLength(256).IsRequired(false);
            entity.Property(chat => chat.Timestamp).IsRequired();
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.ToTable("ChatMessage");
            entity.HasKey(message => message.Id);
            entity.Property(message => message.Role).HasMaxLength(32).IsRequired();
            entity.Property(message => message.Content).IsRequired();
            entity.Property(message => message.Image).HasMaxLength(1024).IsRequired(false);
            entity.Property(message => message.Timestamp).IsRequired();
            entity.HasOne(message => message.Chat)
                .WithMany(chat => chat.Messages)
                .HasForeignKey(message => message.ChatId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(message => new { message.ChatId, message.Timestamp });
        });
    }
}

public sealed class Chat
{
    public Guid Id { get; set; }

    public string? Title { get; set; }

    public DateTime Timestamp { get; set; }

    public ICollection<ChatMessage> Messages { get; set; } = [];
}

public sealed class ChatMessage
{
    public int Id { get; set; }

    public Guid ChatId { get; set; }

    public Chat Chat { get; set; } = null!;

    public string Role { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string? Image { get; set; }

    public DateTime Timestamp { get; set; }
}
