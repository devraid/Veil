using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Veil.Services;

public sealed class AppSettingsStore
{
    private static readonly string StoragePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Veil",
        "openai-settings.dat");

    public OpenAiSettings? GetStoredSettings()
    {
        if (!File.Exists(StoragePath))
        {
            return null;
        }

        try
        {
            var protectedBytes = File.ReadAllBytes(StoragePath);
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
        Save(new OpenAiSettings(apiKey, model, GetStoredSettings()?.LastChatId));
    }

    public void SaveLastChatId(Guid? chatId)
    {
        var settings = GetStoredSettings();
        if (settings is not null)
        {
            Save(settings with { LastChatId = chatId });
        }
    }

    private static void Save(OpenAiSettings settings)
    {
        var directory = Path.GetDirectoryName(StoragePath)
            ?? throw new InvalidOperationException("Could not determine the settings storage directory.");
        Directory.CreateDirectory(directory);

        var plainBytes = JsonSerializer.SerializeToUtf8Bytes(settings);
        var protectedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(StoragePath, protectedBytes);
    }
}

public sealed record OpenAiSettings(string ApiKey, string Model, Guid? LastChatId = null);
