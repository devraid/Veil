using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Extensions.Logging;
using Veil.Data;
using Veil.Services;

namespace Veil
{
    public partial class MainWindow : Window
    {
        private readonly StartupService _startupService;
        private Task<ChatService>? _databaseInitializationTask;
        private readonly SettingsService _settingsService;
        private readonly FrontendMessageSender _frontendMessageSender;
        private readonly WebViewHostService _webViewHostService;
        private readonly ChatRequestService _chatRequestService;
        private readonly ILogger<MainWindow> _logger;
        private readonly WebViewMessageRouter _webViewMessageRouter;
        private ChatService? _chatService;

        public MainWindow(SettingsService settingsService, FrontendMessageSender frontendMessageSender,
            WebViewHostService webViewHostService, StartupService startupService,
            ChatRequestService chatRequestService, ILogger<MainWindow> logger)
        {
            InitializeComponent();
            _settingsService = settingsService;
            _frontendMessageSender = frontendMessageSender;
            _webViewHostService = webViewHostService;
            _startupService = startupService;
            _chatRequestService = chatRequestService;
            _logger = logger;
            _webViewMessageRouter = new WebViewMessageRouter(
                WaitForDatabaseAsync,
                SendApiKeyStatusAsync,
                SendChatsAsync,
                OpenChatAsync,
                StartNewChatAsync,
                DeleteChatAsync,
                RenameChatAsync,
                SaveChatAsync,
                SaveApiKeyAsync);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) => _ = WindowLoadedAsync();

        private async Task WindowLoadedAsync()
        {
            try
            {
                await _webViewHostService.InitializeAsync(
                    FrontendView,
                    coreWebView => coreWebView.WebMessageReceived += CoreWebView2_WebMessageReceived);
                _databaseInitializationTask = _startupService.InitializeAsync();
                _chatService = await _databaseInitializationTask;

            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Veil startup failed.");
                FrontendView.NavigateToString("<h1>Veil could not start</h1><p>Veil could not complete startup.</p>");
            }
        }

        private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e) =>
            _ = RouteWebViewMessageAsync(e.WebMessageAsJson);

        private async Task RouteWebViewMessageAsync(string webMessageJson)
        {
            try
            {
                await _webViewMessageRouter.RouteAsync(webMessageJson);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "WebView message routing failed.");
                await SendToFrontendAsync(new ChatErrorResponse("The request could not be processed."));
            }
        }

        private async Task WaitForDatabaseAsync()
        {
            _chatService ??= await (_databaseInitializationTask ??= _startupService.InitializeAsync());
        }

        private async Task SendApiKeyStatusAsync()
        {
            var storedSettings = _settingsService.GetStoredSettings();
            var configured = _settingsService.IsConfigured();
            await SendToFrontendAsync(new ApiKeyStatusResponse(configured, storedSettings?.Model));
            if (configured)
            {
                await SendChatsAsync();
            }
        }

        private async Task SaveApiKeyAsync(SaveApiKeyCommand message)
        {
            if (!_settingsService.Save(message.ApiKey, message.Model))
            {
                await SendToFrontendAsync(new ApiKeySavedResponse(false, "Enter an API key and model."));
                return;
            }
            await SendToFrontendAsync(new ApiKeySavedResponse(true));
        }

        private async Task SendChatsAsync()
        {
            if (_chatService is null)
            {
                return;
            }

            var chats = await _chatService.GetChatsAsync();
            await SendToFrontendAsync(new ChatsLoadedResponse(chats));

            var lastChatId = _settingsService.GetLastChatId();
            var chatToOpen = lastChatId.HasValue && chats.Any(chat => chat.Id == lastChatId.Value)
                ? lastChatId.Value
                : chats.FirstOrDefault()?.Id ?? Guid.Empty;
            if (_chatService.ActiveChatId == Guid.Empty && chatToOpen != Guid.Empty)
            {
                await OpenChatAsync(chatToOpen);
            }
        }

        private async Task OpenChatAsync(ChatOpenCommand message)
        {
            await OpenChatAsync(message.ChatId);
        }

        private async Task OpenChatAsync(Guid chatId)
        {
            if (_chatService is null)
            {
                return;
            }

            var entries = await _chatService.OpenAsync(chatId);
            if (entries is not null)
            {
                _settingsService.SaveLastChatId(chatId);
                await SendToFrontendAsync(new ChatLoadedResponse(chatId, entries));
            }
        }

        private async Task StartNewChatAsync()
        {
            if (_chatService is null)
            {
                return;
            }

            var chatId = await _chatService.StartNewAsync();
            _settingsService.SaveLastChatId(chatId);
            await SendChatsAsync();
            await SendToFrontendAsync(new ChatLoadedResponse(chatId, Array.Empty<ChatEntryResponse>()));
        }

        private async Task DeleteChatAsync(ChatDeleteCommand message)
        {
            if (_chatService is null)
            {
                return;
            }

            if (await _chatService.DeleteAsync(message.ChatId))
            {
                if (_settingsService.GetLastChatId() == message.ChatId)
                {
                    _settingsService.SaveLastChatId(null);
                }
                await SendChatsAsync();
            }
        }

        private async Task RenameChatAsync(ChatRenameCommand message)
        {
            if (_chatService is null)
            {
                return;
            }

            var title = message.Title.Trim();
            if (!string.IsNullOrWhiteSpace(title) && await _chatService.RenameAsync(message.ChatId, title))
            {
                await SendChatsAsync();
            }
        }

        private async Task SaveChatAsync(ChatSubmitCommand message)
        {
            if (_chatService is null)
            {
                return;
            }

            var text = message.Text?.Trim();
            var imageDataUrl = message.Image;

            if (string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(imageDataUrl))
            {
                return;
            }

            await _chatRequestService.SubmitAsync(message, FrontendView.CoreWebView2);
        }

        private async Task SendToFrontendAsync(object message)
        {
            await _frontendMessageSender.SendAsync(FrontendView.CoreWebView2, message);
        }
    }
}