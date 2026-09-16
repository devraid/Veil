using Microsoft.EntityFrameworkCore;
using Veil.Data;

namespace Veil.Services;

public sealed class StartupService
{
    private readonly TaskCompletionSource<ChatService> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly OpenAiChatService _openAiChatService;
    private readonly ImageDataUrlService _imageDataUrlService;
    private VeilDbContext? _dbContext;

    public StartupService(OpenAiChatService openAiChatService, ImageDataUrlService imageDataUrlService)
    {
        _openAiChatService = openAiChatService;
        _imageDataUrlService = imageDataUrlService;
    }

    public Task<ChatService> InitializeAsync()
    {
        _ = InitializeCoreAsync();
        return _completion.Task;
    }

    private async Task InitializeCoreAsync()
    {
        try
        {
            _dbContext = VeilDbContextFactory.Create();
            await _dbContext.Database.MigrateAsync();
            _completion.SetResult(new ChatService(_dbContext, _openAiChatService, _imageDataUrlService));
        }
        catch (Exception exception)
        {
            _completion.SetException(exception);
        }
    }
}