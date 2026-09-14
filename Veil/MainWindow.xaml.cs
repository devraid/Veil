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
        private readonly AppSettingsStore _appSettingsStore = new();
        private readonly OpenAiChatService _openAiChatService = new();

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
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
            _dbContext = VeilDbContextFactory.Create();
            await ApplyMigrationsAsync(_dbContext);
            FrontendView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

            var frontendUrl = Environment.GetEnvironmentVariable("VEIL_FRONTEND_URL");
            if (!string.IsNullOrWhiteSpace(frontendUrl))
            {
                FrontendView.CoreWebView2.Navigate(frontendUrl);
                return;
            }

            try
            {
                _frontendDirectory = ExtractFrontend();
                FrontendView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "veil.local",
                    _frontendDirectory,
                    CoreWebView2HostResourceAccessKind.Allow);
                FrontendView.CoreWebView2.Navigate("https://veil.local/index.html");
                return;
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                FrontendView.NavigateToString($"<h1>Veil</h1><p>The packaged frontend could not be loaded: {System.Net.WebUtility.HtmlEncode(exception.Message)}</p>");
            }
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
            var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync();
            if (!appliedMigrations.Any() && await HasTableAsync(dbContext, "Chat"))
            {
                var initialMigration = (await dbContext.Database.GetPendingMigrationsAsync()).FirstOrDefault();
                if (initialMigration is not null)
                {
                    await dbContext.Database.ExecuteSqlRawAsync(
                        "CREATE TABLE IF NOT EXISTS \"__EFMigrationsHistory\" (\"MigrationId\" TEXT NOT NULL CONSTRAINT \"PK___EFMigrationsHistory\" PRIMARY KEY, \"ProductVersion\" TEXT NOT NULL);");
                    await dbContext.Database.ExecuteSqlInterpolatedAsync(
                        $"INSERT OR IGNORE INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ({initialMigration}, {"10.0.0"});");
                }
            }

            await dbContext.Database.MigrateAsync();
        }

        private static async Task<bool> HasTableAsync(VeilDbContext dbContext, string tableName)
        {
            await using var connection = dbContext.Database.GetDbConnection();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT EXISTS (SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $tableName);";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "$tableName";
            parameter.Value = tableName;
            command.Parameters.Add(parameter);
            return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
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
                    await SendApiKeyStatusAsync();
                    await SendExistingChatsAsync();
                    break;
                case "chat.submit":
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
                configured = !string.IsNullOrWhiteSpace(storedSettings?.ApiKey) ||
                    !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_API_KEY")),
                model = storedSettings?.Model
            });
        }

        private async Task SaveApiKeyAsync(JsonElement message)
        {
            var apiKey = message.TryGetProperty("apiKey", out var keyProperty)
                ? keyProperty.GetString()?.Trim()
                : null;
            var model = message.TryGetProperty("model", out var modelProperty)
                ? modelProperty.GetString()?.Trim()
                : null;
            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(model))
            {
                await SendToFrontendAsync(new { type = "settings.apiKeySaved", success = false, message = "Enter an API key and model." });
                return;
            }

            _appSettingsStore.Save(apiKey, model);
            await SendToFrontendAsync(new { type = "settings.apiKeySaved", success = true });
        }

        private async Task SendExistingChatsAsync()
        {
            if (_dbContext is null)
            {
                return;
            }

            var chats = await _dbContext.Chats
                .OrderBy(chat => chat.Timestamp)
                .ToListAsync();

            await SendToFrontendAsync(new
            {
                type = "chat.loaded",
                entries = chats.Select(CreateFrontendEntry)
            });
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

            var imagePath = await SaveImageAsync(imageDataUrl);
            var chat = new Chat
            {
                ChatId = Guid.NewGuid(),
                Role = "user",
                Content = text ?? string.Empty,
                Image = imagePath,
                Timestamp = DateTime.UtcNow
            };

            _dbContext.Chats.Add(chat);
            await _dbContext.SaveChangesAsync();
            await SendToFrontendAsync(new { type = "chat.added", entry = CreateFrontendEntry(chat) });

            try
            {
                var conversation = await _dbContext.Chats
                    .Where(savedChat => savedChat.ChatId == chat.ChatId)
                    .OrderBy(savedChat => savedChat.Timestamp)
                    .Select(savedChat => new ChatTurn(savedChat.Role, savedChat.Content, savedChat.Image))
                    .ToListAsync();
                var response = await _openAiChatService.GenerateResponseAsync(conversation);
                var aiChat = new Chat
                {
                    ChatId = chat.ChatId,
                    Role = "ai",
                    Content = response,
                    Timestamp = DateTime.UtcNow
                };

                _dbContext.Chats.Add(aiChat);
                await _dbContext.SaveChangesAsync();
                await SendToFrontendAsync(new { type = "chat.added", entry = CreateFrontendEntry(aiChat) });
            }
            catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
            {
                await SendToFrontendAsync(new { type = "chat.error", message = exception.Message });
            }
        }

        private static object CreateFrontendEntry(Chat chat)
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