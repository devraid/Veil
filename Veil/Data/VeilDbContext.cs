using Microsoft.EntityFrameworkCore;

namespace Veil.Data;

public sealed class VeilDbContext : DbContext
{
    public VeilDbContext(DbContextOptions<VeilDbContext> options)
        : base(options)
    {
    }

    public DbSet<AppSetting> Settings => Set<AppSetting>();

    public DbSet<Chat> Chats => Set<Chat>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppSetting>(entity =>
        {
            entity.HasKey(setting => setting.Key);
            entity.Property(setting => setting.Key).HasMaxLength(128);
            entity.Property(setting => setting.Value).IsRequired();
        });

        modelBuilder.Entity<Chat>(entity =>
        {
            entity.ToTable("Chat");
            entity.HasKey(chat => chat.Id);
            entity.Property(chat => chat.UserText).IsRequired(false);
            entity.Property(chat => chat.Answer).IsRequired();
            entity.Property(chat => chat.Image).HasMaxLength(1024).IsRequired(false);
            entity.Property(chat => chat.Timestamp).IsRequired();
        });
    }
}

public sealed class AppSetting
{
    public required string Key { get; set; }

    public required string Value { get; set; }
}

public sealed class Chat
{
    public int Id { get; set; }

    public string? UserText { get; set; }

    public string Answer { get; set; } = string.Empty;

    public string? Image { get; set; }

    public DateTime Timestamp { get; set; }
}
