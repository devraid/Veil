using System.Net.Http;
using Microsoft.Extensions.Logging;

namespace Veil.Services;

public sealed class ChatRequestService : IDisposable
{
    private readonly StartupService _startupService;
    private readonly ImageStorageService _imageStorageService;
    private readonly FrontendMessageSender _frontendMessageSender;
    private readonly ILogger<ChatRequestService> _logger;
    private readonly ChatRequestCancellation _requestCancellation;

    public ChatRequestService(
        StartupService startupService,
        ImageStorageService imageStorageService,
        FrontendMessageSender frontendMessageSender,
        ChatRequestCancellation requestCancellation,
        ILogger<ChatRequestService> logger)
    {
        _startupService = startupService;
        _imageStorageService = imageStorageService;
        _frontendMessageSender = frontendMessageSender;
        _requestCancellation = requestCancellation;
        _logger = logger;
    }

    public async Task SubmitAsync(ChatSubmitCommand command, Microsoft.Web.WebView2.Core.CoreWebView2 webView)
    {
        var text = command.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(command.Image))
        {
            return;
        }

        var cancellationToken = _requestCancellation.StartNext();
        try
        {
            var chatService = await _startupService.InitializeAsync();
            var imagePath = await _imageStorageService.SaveDataUrlAsync(command.Image, cancellationToken);
            var userMessage = await chatService.SaveUserMessageAsync(text, imagePath);
            await _frontendMessageSender.SendAsync(webView, new ChatAddedResponse(userMessage.UserEntry));

            var aiEntry = await chatService.GenerateAiResponseDtoAsync(userMessage.Conversation, cancellationToken);
            await _frontendMessageSender.SendAsync(webView, new ChatAddedResponse(aiEntry));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Canceled superseded chat request.");
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Chat response generation failed.");
            await _frontendMessageSender.SendAsync(webView, new ChatErrorResponse("The chat request failed."));
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogError(exception, "Chat request was invalid.");
            await _frontendMessageSender.SendAsync(webView, new ChatErrorResponse("The chat request failed."));
        }
    }

    public void Dispose() => _requestCancellation.Dispose();

}
