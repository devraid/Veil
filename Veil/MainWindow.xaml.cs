using System;
using System.IO.Compression;
using System.IO;
using System.Reflection;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Web.WebView2.Core;
using Veil.Data;
using Veil.Services;

namespace Veil
{
    public partial class MainWindow : Window
    {
        private VeilDbContext? _dbContext;
        private string? _frontendDirectory;
        private Task? _databaseInitializationTask;
        private Guid _activeChatId;
        private readonly AppSettingsStore _appSettingsStore = new();
        private readonly OpenAiChatService _openAiChatService = new();

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var frontendUrl = Environment.GetEnvironmentVariable("VEIL_FRONTEND_URL");
                if (string.IsNullOrWhiteSpace(frontendUrl))
                {
                    var webViewDataDirectory = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "Veil",
                        "WebView2");
                    Directory.CreateDirectory(webViewDataDirectory);
                    var webViewEnvironment = await CoreWebView2Environment.CreateAsync(
                        browserExecutableFolder: null,
                        userDataFolder: webViewDataDirectory);
                    await FrontendView.EnsureCoreWebView2Async(webViewEnvironment);
                }
                else
                {
                    await FrontendView.EnsureCoreWebView2Async();
                }

                FrontendView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
                _databaseInitializationTask = InitializeDatabaseAsync();

                if (!string.IsNullOrWhiteSpace(frontendUrl))
                {
                    FrontendView.CoreWebView2.Navigate(frontendUrl);
                }
                else
                {
                    _frontendDirectory = ExtractFrontend();
                    FrontendView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                        "veil.local",
                        _frontendDirectory,
                        CoreWebView2HostResourceAccessKind.Allow);
                    FrontendView.CoreWebView2.Navigate("https://veil.local/index.html");
                }

            }
            catch (Exception exception)
            {
                FrontendView.NavigateToString($"<h1>Veil could not start</h1><p>{System.Net.WebUtility.HtmlEncode(exception.Message)}</p>");
            }
        }

        private async Task InitializeDatabaseAsync()
        {
            _dbContext = VeilDbContextFactory.Create();
            await ApplyMigrationsAsync(_dbContext);
        }

        private static string ExtractFrontend()
        {
            var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("Veil.Frontend.zip")
                ?? throw new InvalidOperationException("The packaged frontend resource is missing.");
            var directory = Path.Combine(Path.GetTempPath(), "Veil", "frontend");
            Directory.CreateDirectory(directory);
            ZipFile.ExtractToDirectory(resource, directory, overwriteFiles: true);
            return directory;
        }

        private static async Task ApplyMigrationsAsync(VeilDbContext dbContext)
        {
            await dbContext.Database.MigrateAsync();
        }

        private async void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            using var message = JsonDocument.Parse(e.WebMessageAsJson);
            var root = message.RootElement;

            if (!root.TryGetProperty("type", out var typeProperty))
            {
                return;
            }

            switch (typeProperty.GetString())
            {
                case "chat.ready":
                    if (_databaseInitializationTask is not null)
                    {
                        await _databaseInitializationTask;
                    }
                    await SendApiKeyStatusAsync();
                    await SendChatsAsync();
                    break;
                case "chat.open":
                    if (_databaseInitializationTask is not null)
                    {
                        await _databaseInitializationTask;
                    }
                    await OpenChatAsync(root);
                    break;
                case "chat.new":
                    if (_databaseInitializationTask is not null)
                    {
                        await _databaseInitializationTask;
                    }
                    await StartNewChatAsync();
                    break;
                case "chat.delete":
                    if (_databaseInitializationTask is not null)
                    {
                        await _databaseInitializationTask;
                    }
                    await DeleteChatAsync(root);
                    break;
                case "chat.rename":
                    if (_databaseInitializationTask is not null)
                    {
                        await _databaseInitializationTask;
                    }
                    await RenameChatAsync(root);
                    break;
                case "chat.submit":
                    if (_databaseInitializationTask is not null)
                    {
                        await _databaseInitializationTask;
                    }
                    await SaveChatAsync(root);
                    break;
                case "settings.saveApiKey":
                    await SaveApiKeyAsync(root);
                    break;
            }
        }

        private async Task SendApiKeyStatusAsync()
        {
            var storedSettings = _appSettingsStore.GetStoredSettings();
            await SendToFrontendAsync(new
            {
                type = "settings.apiKeyStatus",
                configured = IsSettingsConfigured(),
                model = storedSettings?.Model
            });
        }

        private bool IsSettingsConfigured()
        {
            var storedSettings = _appSettingsStore.GetStoredSettings();
            if (!string.IsNullOrWhiteSpace(storedSettings?.ApiKey) &&
                !string.IsNullOrWhiteSpace(storedSettings.Model))
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_API_KEY")) &&
                !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_MODEL"));
        }

        private async Task SaveApiKeyAsync(JsonElement message)
        {
            var enteredApiKey = message.TryGetProperty("apiKey", out var keyProperty)
                ? keyProperty.GetString()?.Trim()
                : null;
            var model = message.TryGetProperty("model", out var modelProperty)
                ? modelProperty.GetString()?.Trim()
                : null;
            var existingSettings = _appSettingsStore.GetStoredSettings();
            var apiKey = string.IsNullOrWhiteSpace(enteredApiKey)
                ? existingSettings?.ApiKey
                : enteredApiKey;
            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(model))
            {
                await SendToFrontendAsync(new { type = "settings.apiKeySaved", success = false, message = "Enter an API key and model." });
                return;
            }

            _appSettingsStore.Save(apiKey, model);
            await SendToFrontendAsync(new { type = "settings.apiKeySaved", success = true });
        }

        private async Task SendChatsAsync()
        {
            if (_dbContext is null)
            {
                return;
            }

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
                    chat.Title = firstMessage.Content.Length > 60
                        ? firstMessage.Content[..60]
                        : firstMessage.Content;
                }
            }

            await _dbContext.SaveChangesAsync();

            await SendToFrontendAsync(new
            {
                type = "chats.loaded",
                chats = chats.Select(chat => new { id = chat.Id, chat.Title, chat.Timestamp })
            });

            if (_activeChatId == Guid.Empty && chats.Count > 0)
            {
                await OpenChatAsync(chats[0].Id);
            }
        }

        private async Task OpenChatAsync(JsonElement message)
        {
            if (message.TryGetProperty("chatId", out var chatIdProperty) &&
                Guid.TryParse(chatIdProperty.GetString(), out var chatId))
            {
                await OpenChatAsync(chatId);
            }
        }

        private async Task OpenChatAsync(Guid chatId)
        {
            if (_dbContext is null || !await _dbContext.Chats.AnyAsync(chat => chat.Id == chatId))
            {
                return;
            }

            _activeChatId = chatId;
            var messages = await _dbContext.ChatMessages
                .Where(message => message.ChatId == chatId)
                .OrderBy(message => message.Timestamp)
                .ToListAsync();
            await SendToFrontendAsync(new
            {
                type = "chat.loaded",
                chatId,
                entries = messages.Select(CreateFrontendEntry)
            });
        }

        private async Task StartNewChatAsync()
        {
            if (_dbContext is null)
            {
                return;
            }

            var chat = new Chat { Id = Guid.NewGuid(), Timestamp = DateTime.UtcNow };
            _dbContext.Chats.Add(chat);
            await _dbContext.SaveChangesAsync();
            _activeChatId = chat.Id;
            await SendChatsAsync();
            await SendToFrontendAsync(new { type = "chat.loaded", chatId = chat.Id, entries = Array.Empty<object>() });
        }

        private async Task DeleteChatAsync(JsonElement message)
        {
            if (_dbContext is null ||
                !message.TryGetProperty("chatId", out var chatIdProperty) ||
                !Guid.TryParse(chatIdProperty.GetString(), out var chatId))
            {
                return;
            }

            var chat = await _dbContext.Chats.FindAsync(chatId);
            if (chat is null)
            {
                return;
            }

            _dbContext.Chats.Remove(chat);
            await _dbContext.SaveChangesAsync();
            if (_activeChatId == chatId)
            {
                _activeChatId = Guid.Empty;
            }
            await SendChatsAsync();
        }

        private async Task RenameChatAsync(JsonElement message)
        {
            if (_dbContext is null ||
                !message.TryGetProperty("chatId", out var chatIdProperty) ||
                !Guid.TryParse(chatIdProperty.GetString(), out var chatId) ||
                !message.TryGetProperty("title", out var titleProperty))
            {
                return;
            }

            var title = titleProperty.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                return;
            }

            var chat = await _dbContext.Chats.FindAsync(chatId);
            if (chat is null)
            {
                return;
            }

            chat.Title = title.Length > 256 ? title[..256] : title;
            await _dbContext.SaveChangesAsync();
            await SendChatsAsync();
        }

        private async Task SaveChatAsync(JsonElement message)
        {
            if (_dbContext is null)
            {
                return;
            }

            var text = message.TryGetProperty("text", out var textProperty)
                ? textProperty.GetString()?.Trim()
                : null;
            var imageDataUrl = message.TryGetProperty("image", out var imageProperty)
                ? imageProperty.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(imageDataUrl))
            {
                return;
            }

            if (_activeChatId == Guid.Empty)
            {
                await StartNewChatAsync();
            }

            var chatConversation = await _dbContext.Chats.FindAsync(_activeChatId);
            if (chatConversation is not null &&
                string.IsNullOrWhiteSpace(chatConversation.Title) &&
                !string.IsNullOrWhiteSpace(text))
            {
                chatConversation.Title = text.Length > 60 ? text[..60] : text;
            }

            var imagePath = await SaveImageAsync(imageDataUrl);
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
            await SendChatsAsync();
            await SendToFrontendAsync(new { type = "chat.added", entry = CreateFrontendEntry(chat) });

            try
            {
                var conversation = await _dbContext.ChatMessages
                    .Where(savedChat => savedChat.ChatId == chat.ChatId)
                    .OrderBy(savedChat => savedChat.Timestamp)
                    .Select(savedChat => new ChatTurn(savedChat.Role, savedChat.Content, savedChat.Image))
                    .ToListAsync();
                var response = await _openAiChatService.GenerateResponseAsync(conversation);
                var aiChat = new ChatMessage
                {
                    ChatId = chat.ChatId,
                    Role = "ai",
                    Content = response,
                    Timestamp = DateTime.UtcNow
                };

                _dbContext.ChatMessages.Add(aiChat);
                await _dbContext.SaveChangesAsync();
                await SendToFrontendAsync(new { type = "chat.added", entry = CreateFrontendEntry(aiChat) });
            }
            catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
            {
                await SendToFrontendAsync(new { type = "chat.error", message = exception.Message });
            }
        }

        private static object CreateFrontendEntry(ChatMessage chat)
        {
            return new
            {
                chat.Id,
                UserText = chat.Role == "user" ? chat.Content : null,
                Answer = chat.Role == "ai" ? chat.Content : string.Empty,
                chat.Timestamp,
                Image = ReadImageAsDataUrl(chat.Image)
            };
        }

        private static async Task<string?> SaveImageAsync(string? imageDataUrl)
        {
            if (string.IsNullOrWhiteSpace(imageDataUrl) ||
                !imageDataUrl.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var separatorIndex = imageDataUrl.IndexOf(",", StringComparison.Ordinal);
            if (separatorIndex < 0)
            {
                return null;
            }

            var header = imageDataUrl[..separatorIndex];
            var base64Data = imageDataUrl[(separatorIndex + 1)..];
            var extension = GetImageExtension(header);
            if (extension is null)
            {
                return null;
            }

            byte[] imageBytes;
            try
            {
                imageBytes = Convert.FromBase64String(base64Data);
            }
            catch (FormatException)
            {
                return null;
            }

            var imageDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Veil",
                "Images");
            Directory.CreateDirectory(imageDirectory);

            var imagePath = Path.Combine(imageDirectory, $"{Guid.NewGuid():N}{extension}");
            await File.WriteAllBytesAsync(imagePath, imageBytes);
            return imagePath;
        }

        private static string? ReadImageAsDataUrl(string? imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                return null;
            }

            var extension = Path.GetExtension(imagePath).ToLowerInvariant();
            var mimeType = extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => null
            };

            if (mimeType is null)
            {
                return null;
            }

            var imageData = Convert.ToBase64String(File.ReadAllBytes(imagePath));
            return $"data:{mimeType};base64,{imageData}";
        }

        private static string? GetImageExtension(string dataUrlHeader)
        {
            return dataUrlHeader.ToLowerInvariant() switch
            {
                "data:image/jpeg;base64" => ".jpg",
                "data:image/png;base64" => ".png",
                "data:image/gif;base64" => ".gif",
                "data:image/webp;base64" => ".webp",
                _ => null
            };
        }

        private async Task SendToFrontendAsync(object message)
        {
            var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            await FrontendView.ExecuteScriptAsync($"window.veilChat?.receive({json});");
        }
    }
}