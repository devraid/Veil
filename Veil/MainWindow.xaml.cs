using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Web.WebView2.Core;
using Veil.Data;

namespace Veil
{
    public partial class MainWindow : Window
    {
        private VeilDbContext? _dbContext;

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await FrontendView.EnsureCoreWebView2Async();
            _dbContext = VeilDbContextFactory.Create();
            await ApplyMigrationsAsync(_dbContext);
            FrontendView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

            var frontendUrl = Environment.GetEnvironmentVariable("VEIL_FRONTEND_URL");
            if (!string.IsNullOrWhiteSpace(frontendUrl))
            {
                FrontendView.CoreWebView2.Navigate(frontendUrl);
                return;
            }

            var frontendFile = Path.Combine(
                AppContext.BaseDirectory,
                "frontend",
                "dist",
                "index.html");

            if (File.Exists(frontendFile))
            {
                FrontendView.CoreWebView2.Navigate(new Uri(frontendFile).AbsoluteUri);
                return;
            }

            FrontendView.NavigateToString("<h1>Veil</h1><p>The frontend is not available. Start the Vite development server or build the frontend.</p>");
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
                    await SendExistingChatsAsync();
                    break;
                case "chat.submit":
                    await SaveChatAsync(root);
                    break;
            }
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
                UserText = string.IsNullOrWhiteSpace(text) ? null : text,
                Image = imagePath,
                Timestamp = DateTime.UtcNow
            };

            _dbContext.Chats.Add(chat);
            await _dbContext.SaveChangesAsync();
            await SendToFrontendAsync(new { type = "chat.added", entry = CreateFrontendEntry(chat) });
        }

        private static object CreateFrontendEntry(Chat chat)
        {
            return new
            {
                chat.Id,
                chat.UserText,
                chat.Answer,
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