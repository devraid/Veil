using Microsoft.EntityFrameworkCore;
using Veil.Data;

namespace Veil.Services;

public sealed class StartupService
{
    private readonly Lazy<Task<ChatService>> _initialization;
    private readonly OpenAiChatService _openAiChatService;
    private readonly ImageDataUrlService _imageDataUrlService;

    public StartupService(OpenAiChatService openAiChatService, ImageDataUrlService imageDataUrlService)
    {
        _openAiChatService = openAiChatService;
        _imageDataUrlService = imageDataUrlService;
        _initialization = new Lazy<Task<ChatService>>(InitializeCoreAsync, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public Task<ChatService> InitializeAsync() => _initialization.Value;

    private async Task<ChatService> InitializeCoreAsync()
    {
        var dbContext = VeilDbContextFactory.Create();
        await dbContext.Database.MigrateAsync();
        return new ChatService(dbContext, _openAiChatService, _imageDataUrlService);
    }
}