namespace Veil.Services;

public sealed class SettingsService
{
    private readonly AppSettingsStore _store;

    public SettingsService(AppSettingsStore store)
    {
        _store = store;
    }

    public OpenAiSettings? GetStoredSettings() => _store.GetStoredSettings();

    public bool IsConfigured()
    {
        var settings = _store.GetStoredSettings();
        return (!string.IsNullOrWhiteSpace(settings?.ApiKey) && !string.IsNullOrWhiteSpace(settings.Model)) ||
            (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_API_KEY")) &&
             !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_MODEL")));
    }

    public bool Save(string? enteredApiKey, string? model)
    {
        var existingSettings = _store.GetStoredSettings();
        var apiKey = string.IsNullOrWhiteSpace(enteredApiKey) ? existingSettings?.ApiKey : enteredApiKey.Trim();
        var normalizedModel = model?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(normalizedModel))
        {
            return false;
        }

        _store.Save(apiKey, normalizedModel);
        return true;
    }
}