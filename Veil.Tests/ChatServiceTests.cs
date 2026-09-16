using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Veil.Data;
using Veil.Services;

namespace Veil.Tests;

public sealed class ChatServiceTests
{
    [Fact]
    public async Task OpenAsync_LoadsTheRequestedSavedChat()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync();
        var selectedChatId = Guid.NewGuid();
        var otherChatId = Guid.NewGuid();
        dbContext.Chats.AddRange(
            new Chat { Id = selectedChatId, Timestamp = DateTime.UtcNow },
            new Chat { Id = otherChatId, Timestamp = DateTime.UtcNow });
        dbContext.ChatMessages.AddRange(
            new ChatMessage
            {
                ChatId = selectedChatId,
                Role = "user",
                Content = "Selected chat message",
                Timestamp = DateTime.UtcNow
            },
            new ChatMessage
            {
                ChatId = otherChatId,
                Role = "user",
                Content = "Other chat message",
                Timestamp = DateTime.UtcNow
            });
        await dbContext.SaveChangesAsync();
        var service = CreateChatService(dbContext);

        var entries = await service.OpenAsync(selectedChatId);

        Assert.NotNull(entries);
        var entry = Assert.Single(entries);
        Assert.Equal("Selected chat message", entry.UserText);
        Assert.Equal(selectedChatId, service.ActiveChatId);
    }

    [Fact]
    public async Task OpenAsync_ReturnsNullForAMissingSavedChat()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync();
        var service = CreateChatService(dbContext);

        var entries = await service.OpenAsync(Guid.NewGuid());

        Assert.Null(entries);
        Assert.Equal(Guid.Empty, service.ActiveChatId);
    }

    [Fact]
    public async Task ChatSummary_IsPersistedWithItsProcessedMessageCount()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync();
        var chatId = Guid.NewGuid();
        dbContext.Chats.Add(new Chat
        {
            Id = chatId,
            Summary = "User is planning a context budget.",
            SummaryMessageCount = 4,
            Timestamp = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var chat = await dbContext.Chats.SingleAsync(savedChat => savedChat.Id == chatId);

        Assert.Equal("User is planning a context budget.", chat.Summary);
        Assert.Equal(4, chat.SummaryMessageCount);
    }

    private static ChatService CreateChatService(VeilDbContext dbContext)
    {
        var settingsService = new SettingsService(new AppSettingsStore());
        return new ChatService(
            dbContext,
            new OpenAiChatService(settingsService, new ImageDataUrlService()),
            new ImageDataUrlService(),
            settingsService);
    }

    private static VeilDbContext CreateDbContext(SqliteConnection connection) => new(
        new DbContextOptionsBuilder<VeilDbContext>()
            .UseSqlite(connection)
            .Options);
}
