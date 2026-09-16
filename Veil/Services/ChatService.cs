using Microsoft.EntityFrameworkCore;
using Veil.Data;

namespace Veil.Services;

public sealed class ChatService
{
    public sealed record ChatSummary(Guid Id, string? Title, DateTime Timestamp);
    private readonly VeilDbContext _dbContext;
    private readonly OpenAiChatService _openAiChatService;
    private readonly ImageDataUrlService _imageDataUrlService;
    private readonly SettingsService _settingsService;
    private Guid _activeChatId;

    public ChatService(
        VeilDbContext dbContext,
        OpenAiChatService openAiChatService,
        ImageDataUrlService imageDataUrlService,
        SettingsService settingsService)
    {
        _dbContext = dbContext;
        _openAiChatService = openAiChatService;
        _imageDataUrlService = imageDataUrlService;
        _settingsService = settingsService;
    }

    public async Task<ChatEntryResponse> GenerateAiResponseDtoAsync(IReadOnlyList<ChatTurn> conversation, CancellationToken cancellationToken = default)
    {
        var settings = _settingsService.GetEffectiveSettings();
        var recentMessageCount = settings.MaxRecentMessages;
        var chat = await _dbContext.Chats.FindAsync([_activeChatId], cancellationToken)
            ?? throw new InvalidOperationException("The active chat no longer exists.");
        var summarizedMessageCount = Math.Min(chat.SummaryMessageCount, conversation.Count);
        var messagesToSummarize = conversation
            .Skip(summarizedMessageCount)
            .Take(Math.Max(0, conversation.Count - recentMessageCount - summarizedMessageCount))
            .ToList();
        if (messagesToSummarize.Count > 0)
        {
            chat.Summary = await _openAiChatService.GenerateSummaryAsync(
                chat.Summary,
                messagesToSummarize,
                cancellationToken);
            chat.SummaryMessageCount += messagesToSummarize.Count;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var requestContext = conversation.TakeLast(recentMessageCount).ToList();
        if (!string.IsNullOrWhiteSpace(settings.PromptInstructions))
        {
            requestContext.Insert(0, new ChatTurn("system", settings.PromptInstructions, null));
        }
        if (!string.IsNullOrWhiteSpace(chat.Summary))
        {
            requestContext.Insert(1, new ChatTurn("system", $"Conversation summary:\n{chat.Summary}", null));
        }

        var response = await _openAiChatService.GenerateResponseAsync(requestContext, cancellationToken);
        var aiChat = new ChatMessage
        {
            ChatId = _activeChatId,
            Role = "ai",
            Content = response,
            Timestamp = DateTime.UtcNow
        };
        _dbContext.ChatMessages.Add(aiChat);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return CreateFrontendEntry(aiChat);
    }

    public Guid ActiveChatId => _activeChatId;

    public async Task<IReadOnlyList<ChatSummary>> GetChatsAsync()
    {
        var chats = await _dbContext.Chats
            .Include(chat => chat.Messages)
            .OrderByDescending(chat => chat.Timestamp)
            .ToListAsync();

        foreach (var chat in chats.Where(chat => string.IsNullOrWhiteSpace(chat.Title)))
        {
            var firstMessage = chat.Messages
                .Where(message => message.Role == "user" && !string.IsNullOrWhiteSpace(message.Content))
                .OrderBy(message => message.Timestamp)
                .FirstOrDefault();
            if (firstMessage is not null)
            {
                chat.Title = CreateChatTitle(firstMessage.Content);
            }
        }

        await _dbContext.SaveChangesAsync();
        return chats.Select(chat => new ChatSummary(chat.Id, chat.Title, chat.Timestamp)).ToList();
    }

    public async Task<IReadOnlyList<ChatEntryResponse>?> OpenAsync(Guid chatId)
    {
        if (!await _dbContext.Chats.AnyAsync(chat => chat.Id == chatId))
        {
            return null;
        }

        _activeChatId = chatId;
        var messages = await _dbContext.ChatMessages
            .Where(message => message.ChatId == chatId)
            .OrderBy(message => message.Timestamp)
            .ToListAsync();
        return messages.Select(CreateFrontendEntry).ToList();
    }

    public async Task<Guid> StartNewAsync()
    {
        var chat = new Chat { Id = Guid.NewGuid(), Timestamp = DateTime.UtcNow };
        _dbContext.Chats.Add(chat);
        await _dbContext.SaveChangesAsync();
        _activeChatId = chat.Id;
        return chat.Id;
    }

    public async Task<bool> DeleteAsync(Guid chatId)
    {
        var chat = await _dbContext.Chats.FindAsync(chatId);
        if (chat is null)
        {
            return false;
        }

        _dbContext.Chats.Remove(chat);
        await _dbContext.SaveChangesAsync();
        if (_activeChatId == chatId)
        {
            _activeChatId = Guid.Empty;
        }

        return true;
    }

    public async Task<bool> RenameAsync(Guid chatId, string title)
    {
        var chat = await _dbContext.Chats.FindAsync(chatId);
        if (chat is null)
        {
            return false;
        }

        chat.Title = title.Length > 256 ? title[..256] : title;
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<(ChatEntryResponse UserEntry, IReadOnlyList<ChatTurn> Conversation)> SaveUserMessageAsync(
        string? text,
        string? imagePath)
    {
        if (_activeChatId == Guid.Empty)
        {
            await StartNewAsync();
        }

        var chatConversation = await _dbContext.Chats.FindAsync(_activeChatId);
        if (chatConversation is not null && string.IsNullOrWhiteSpace(chatConversation.Title) && !string.IsNullOrWhiteSpace(text))
        {
            chatConversation.Title = CreateChatTitle(text);
        }

        var chat = new ChatMessage
        {
            ChatId = _activeChatId,
            Role = "user",
            Content = text ?? string.Empty,
            Image = imagePath,
            Timestamp = DateTime.UtcNow
        };
        _dbContext.ChatMessages.Add(chat);
        await _dbContext.SaveChangesAsync();

        var conversation = await _dbContext.ChatMessages
            .Where(savedChat => savedChat.ChatId == chat.ChatId)
            .OrderBy(savedChat => savedChat.Timestamp)
            .Select(savedChat => new ChatTurn(savedChat.Role, savedChat.Content, savedChat.Image))
            .ToListAsync();
        return (CreateFrontendEntry(chat), conversation);
    }

    private static string CreateChatTitle(string text) => text.Length > 60 ? text[..60] : text;

    private ChatEntryResponse CreateFrontendEntry(ChatMessage chat) => new(
        chat.Id, chat.Role, chat.Role == "user" ? chat.Content : null,
        chat.Role == "ai" ? chat.Content : string.Empty, chat.Timestamp,
        _imageDataUrlService.ReadAsDataUrl(chat.Image));
}
