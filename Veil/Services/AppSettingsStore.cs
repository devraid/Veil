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
        var directory = Path.GetDirectoryName(StoragePath)
            ?? throw new InvalidOperationException("Could not determine the settings storage directory.");
        Directory.CreateDirectory(directory);

        var plainBytes = JsonSerializer.SerializeToUtf8Bytes(new OpenAiSettings(apiKey, model));
        var protectedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(StoragePath, protectedBytes);
    }
}

public sealed record OpenAiSettings(string ApiKey, string Model);
