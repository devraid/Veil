using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Veil.Services;

public sealed class AppSettingsStore
{
    public const int DefaultMaxRecentMessages = 20;
    public const string DefaultAnswerLength = "Balanced";
    public const string DefaultModel = "gpt-4o-mini";
    private static readonly string DefaultStoragePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Veil",
        "openai-settings.dat");
    private readonly string _storagePath;

    public AppSettingsStore(string? storagePath = null)
    {
        _storagePath = storagePath ?? DefaultStoragePath;
    }

    public OpenAiSettings? GetStoredSettings()
    {
        if (!File.Exists(_storagePath))
        {
            return null;
        }

        try
        {
            var protectedBytes = File.ReadAllBytes(_storagePath);
            var plainBytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<OpenAiSettings>(plainBytes);
        }
        catch (CryptographicException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void Save(string apiKey, string model)
    {
        var existingSettings = GetStoredSettings();
        Save(new OpenAiSettings(
            apiKey,
            model,
            existingSettings?.LastChatId,
            existingSettings?.PromptInstructions ?? string.Empty,
            existingSettings?.MaxRecentMessages ?? DefaultMaxRecentMessages,
            existingSettings?.AnswerLength ?? DefaultAnswerLength));
    }

    public void Save(string apiKey, string model, string? promptInstructions, int? maxRecentMessages, string? answerLength)
    {
        var existingSettings = GetStoredSettings();
        Save(new OpenAiSettings(
            apiKey,
            model,
            existingSettings?.LastChatId,
            promptInstructions?.Trim() ?? string.Empty,
            maxRecentMessages ?? DefaultMaxRecentMessages,
            answerLength ?? DefaultAnswerLength));
    }

    public void SaveLastChatId(Guid? chatId)
    {
        var settings = GetStoredSettings();
        if (settings is not null)
        {
            Save(settings with { LastChatId = chatId });
        }
    }

    public void Save(OpenAiSettings settings) => SaveSettings(settings);

    private void SaveSettings(OpenAiSettings settings)
    {
        var directory = Path.GetDirectoryName(_storagePath)
            ?? throw new InvalidOperationException("Could not determine the settings storage directory.");
        Directory.CreateDirectory(directory);

        var plainBytes = JsonSerializer.SerializeToUtf8Bytes(settings);
        var protectedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_storagePath, protectedBytes);
    }
}

public sealed record OpenAiSettings(
    string ApiKey,
    string Model,
    Guid? LastChatId = null,
    string PromptInstructions = "",
    int MaxRecentMessages = AppSettingsStore.DefaultMaxRecentMessages,
    string AnswerLength = AppSettingsStore.DefaultAnswerLength);
