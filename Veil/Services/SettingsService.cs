using System.IO;

namespace Veil.Services;

public sealed class SettingsService
{
    private readonly AppSettingsStore _store;
    private readonly string _environmentFilePath;

    public SettingsService(
        AppSettingsStore store,
        string? environmentFilePath = null,
        string? applicationBaseDirectory = null)
    {
        _store = store;
        _environmentFilePath = environmentFilePath ?? FindEnvironmentFilePath(applicationBaseDirectory ?? AppContext.BaseDirectory);
    }

    public OpenAiSettings? GetStoredSettings() => _store.GetStoredSettings();

    public OpenAiSettings GetEffectiveSettings()
    {
        var storedSettings = _store.GetStoredSettings();
        if (HasApiKey(storedSettings))
        {
            return storedSettings! with { AnswerLength = NormalizeAnswerLength(storedSettings.AnswerLength) };
        }

        if (HasDevelopmentSettingsFile())
        {
            return GetDevelopmentSettings();
        }

        if (storedSettings is not null)
        {
            return storedSettings with { AnswerLength = NormalizeAnswerLength(storedSettings.AnswerLength) };
        }

        return new OpenAiSettings(string.Empty, AppSettingsStore.DefaultModel);
    }

    public void InitializeDevelopmentSettings()
    {
        if (HasDevelopmentSettingsFile() && !HasApiKey(_store.GetStoredSettings()))
        {
            _store.Save(GetDevelopmentSettings() with { LastChatId = _store.GetStoredSettings()?.LastChatId });
        }
    }

    public Guid? GetLastChatId() => _store.GetStoredSettings()?.LastChatId;

    public void SaveLastChatId(Guid? chatId) => _store.SaveLastChatId(chatId);

    public bool IsConfigured()
    {
        return HasApiKey(_store.GetStoredSettings()) ||
            (HasDevelopmentSettingsFile() && !string.IsNullOrWhiteSpace(GetEnvironmentValue("OPENAI_API_KEY")));
    }

    public bool Save(
        string? enteredApiKey,
        string? model,
        string? promptInstructions = null,
        int? maxRecentMessages = null,
        string? answerLength = null)
    {
        var existingSettings = _store.GetStoredSettings();
        var apiKey = string.IsNullOrWhiteSpace(enteredApiKey) ? existingSettings?.ApiKey : enteredApiKey.Trim();
        var normalizedModel = string.IsNullOrWhiteSpace(model) ? AppSettingsStore.DefaultModel : model.Trim();
        if (string.IsNullOrWhiteSpace(apiKey) ||
            (maxRecentMessages.HasValue && maxRecentMessages.Value is < 1 or > 100) ||
            (answerLength is not null && answerLength is not ("Short" or "Balanced" or "Advanced")))
        {
            return false;
        }

        _store.Save(apiKey, normalizedModel, promptInstructions, maxRecentMessages, answerLength);
        return true;
    }

    private OpenAiSettings GetDevelopmentSettings() => new(
        GetEnvironmentValue("OPENAI_API_KEY") ?? string.Empty,
        GetEnvironmentValue("OPENAI_MODEL") ?? AppSettingsStore.DefaultModel,
        PromptInstructions: GetEnvironmentValue("OPENAI_PROMPT_INSTRUCTIONS") ?? string.Empty,
        MaxRecentMessages: GetMaxRecentMessagesFromEnvironment(),
        AnswerLength: GetAnswerLengthFromEnvironment());

    private bool HasDevelopmentSettingsFile() => File.Exists(_environmentFilePath);

    private static bool HasApiKey(OpenAiSettings? settings) => !string.IsNullOrWhiteSpace(settings?.ApiKey);

    private static string NormalizeAnswerLength(string? answerLength) => answerLength switch
    {
        "Short" => "Short",
        "Advanced" or "Large" => "Advanced",
        _ => AppSettingsStore.DefaultAnswerLength
    };

    private int GetMaxRecentMessagesFromEnvironment()
    {
        return int.TryParse(GetEnvironmentValue("OPENAI_MAX_RECENT_MESSAGES"), out var value) && value is >= 1 and <= 100
            ? value
            : AppSettingsStore.DefaultMaxRecentMessages;
    }

    private string GetAnswerLengthFromEnvironment()
    {
        var value = GetEnvironmentValue("OPENAI_ANSWER_LENGTH");
        return value is "Short" or "Balanced" or "Advanced"
            ? value
            : AppSettingsStore.DefaultAnswerLength;
    }

    private string? GetEnvironmentValue(string name)
    {
        if (!File.Exists(_environmentFilePath))
        {
            return null;
        }

        foreach (var line in File.ReadLines(_environmentFilePath))
        {
            var trimmedLine = line.Trim();
            if (trimmedLine.Length == 0 || trimmedLine.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = trimmedLine.IndexOf('=');
            if (separatorIndex > 0 && string.Equals(trimmedLine[..separatorIndex].Trim(), name, StringComparison.Ordinal))
            {
                return trimmedLine[(separatorIndex + 1)..].Trim().Trim('\"', '\'');
            }
        }

        return null;
    }

    private static string FindEnvironmentFilePath(string applicationBaseDirectory)
    {
        for (var directory = new DirectoryInfo(applicationBaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (directory.EnumerateFiles("*.slnx").Any())
            {
                return Path.Combine(directory.FullName, ".env");
            }
        }

        return Path.Combine(applicationBaseDirectory, ".env");
    }
}