using Veil.Services;

namespace Veil.Tests;

public sealed class SettingsServiceTests
{
    private static AppSettingsStore CreateStore(out string storagePath)
    {
        storagePath = Path.Combine(Path.GetTempPath(), $"Veil-{Guid.NewGuid():N}.dat");
        return new AppSettingsStore(storagePath);
    }

    [Fact]
    public void GetEffectiveSettings_FindsTheEnvironmentFileInAnAncestorDirectory()
    {
        var repositoryPath = Path.Combine(Path.GetTempPath(), $"Veil-{Guid.NewGuid():N}");
        var applicationBaseDirectory = Path.Combine(repositoryPath, "Veil", "bin", "Debug");
        var environmentFilePath = Path.Combine(repositoryPath, ".env");
        var outputEnvironmentFilePath = Path.Combine(applicationBaseDirectory, ".env");
        try
        {
            Directory.CreateDirectory(applicationBaseDirectory);
            File.WriteAllText(Path.Combine(repositoryPath, "Veil.slnx"), string.Empty);
            File.WriteAllText(environmentFilePath, "OPENAI_API_KEY=sk-test\nOPENAI_MODEL=gpt-test");
            File.WriteAllText(outputEnvironmentFilePath, "OPENAI_API_KEY=stale-key\nOPENAI_MODEL=stale-model");
            var service = new SettingsService(CreateStore(out var storagePath), applicationBaseDirectory: applicationBaseDirectory);

            var settings = service.GetEffectiveSettings();

            Assert.Equal("sk-test", settings.ApiKey);
            Assert.Equal("gpt-test", settings.Model);
            File.Delete(storagePath);
        }
        finally
        {
            Directory.Delete(repositoryPath, recursive: true);
        }
    }

    [Fact]
    public void InitializeDevelopmentSettings_DoesNotReplaceASavedApiKey()
    {
        var environmentFilePath = Path.Combine(Path.GetTempPath(), $"Veil-{Guid.NewGuid():N}.env");
        var storagePath = Path.Combine(Path.GetTempPath(), $"Veil-{Guid.NewGuid():N}.dat");
        try
        {
            File.WriteAllText(environmentFilePath, "OPENAI_MODEL=env-model");
            var store = new AppSettingsStore(storagePath);
            var savedSettings = new OpenAiSettings("saved-key", "saved-model");
            store.Save(savedSettings);
            var service = new SettingsService(store, environmentFilePath);

            service.InitializeDevelopmentSettings();

            Assert.True(service.IsConfigured());
            Assert.Equal(savedSettings, service.GetEffectiveSettings());
        }
        finally
        {
            File.Delete(environmentFilePath);
            File.Delete(storagePath);
        }
    }

    [Fact]
    public void IsConfigured_ReturnsFalseWhenTheSolutionEnvironmentFileHasNoApiKey()
    {
        var repositoryPath = Path.Combine(Path.GetTempPath(), $"Veil-{Guid.NewGuid():N}");
        var applicationBaseDirectory = Path.Combine(repositoryPath, "Veil", "bin", "Debug");
        try
        {
            Directory.CreateDirectory(applicationBaseDirectory);
            File.WriteAllText(Path.Combine(repositoryPath, "Veil.slnx"), string.Empty);
            File.WriteAllText(Path.Combine(repositoryPath, ".env"), "OPENAI_MODEL=gpt-test");
            var service = new SettingsService(CreateStore(out var storagePath), applicationBaseDirectory: applicationBaseDirectory);

            Assert.False(service.IsConfigured());
            File.Delete(storagePath);
        }
        finally
        {
            Directory.Delete(repositoryPath, recursive: true);
        }
    }

    [Fact]
    public void InitializeDevelopmentSettings_PersistsResolvedSettingsAndPreservesTheLastChat()
    {
        var environmentFilePath = Path.Combine(Path.GetTempPath(), $"Veil-{Guid.NewGuid():N}.env");
        var storagePath = Path.Combine(Path.GetTempPath(), $"Veil-{Guid.NewGuid():N}.dat");
        var lastChatId = Guid.NewGuid();
        try
        {
            File.WriteAllText(environmentFilePath, "OPENAI_API_KEY=sk-test\nOPENAI_PROMPT_INSTRUCTIONS=Be concise.\nOPENAI_MAX_RECENT_MESSAGES=12");
            var store = new AppSettingsStore(storagePath);
            store.Save(new OpenAiSettings(string.Empty, "old-model", lastChatId));
            var service = new SettingsService(store, environmentFilePath);

            service.InitializeDevelopmentSettings();

            Assert.Equal(new OpenAiSettings("sk-test", AppSettingsStore.DefaultModel, lastChatId, "Be concise.", 12, AppSettingsStore.DefaultAnswerLength), store.GetStoredSettings());
        }
        finally
        {
            File.Delete(environmentFilePath);
            File.Delete(storagePath);
        }
    }

    [Fact]
    public void IsConfigured_ReturnsFalseWhenTheEnvironmentFileIsAbsent()
    {
        var environmentFilePath = Path.Combine(Path.GetTempPath(), $"Veil-{Guid.NewGuid():N}.env");
        var store = CreateStore(out var storagePath);
        var service = new SettingsService(store, environmentFilePath);

        Assert.False(service.IsConfigured());
        File.Delete(storagePath);
    }

    [Fact]
    public void GetEffectiveSettings_UsesEnvironmentValuesAndDefaultsWhenTheEnvironmentFileExists()
    {
        var environmentFilePath = Path.Combine(Path.GetTempPath(), $"Veil-{Guid.NewGuid():N}.env");
        try
        {
            File.WriteAllText(environmentFilePath, "OPENAI_API_KEY=sk-test\nOPENAI_MAX_RECENT_MESSAGES=12\nOPENAI_ANSWER_LENGTH=Advanced");
            var service = new SettingsService(CreateStore(out var storagePath), environmentFilePath);

            var settings = service.GetEffectiveSettings();

            Assert.Equal("sk-test", settings.ApiKey);
            Assert.Equal(AppSettingsStore.DefaultModel, settings.Model);
            Assert.Equal(12, settings.MaxRecentMessages);
            Assert.Equal("Advanced", settings.AnswerLength);
            File.Delete(storagePath);
        }
        finally
        {
            File.Delete(environmentFilePath);
        }
    }

    [Fact]
    public void IsConfigured_RequiresAnApiKeyWhenTheEnvironmentFileExists()
    {
        var environmentFilePath = Path.Combine(Path.GetTempPath(), $"Veil-{Guid.NewGuid():N}.env");
        try
        {
            File.WriteAllText(environmentFilePath, "OPENAI_MODEL=gpt-test");
            var service = new SettingsService(CreateStore(out var storagePath), environmentFilePath);

            Assert.False(service.IsConfigured());
            File.Delete(storagePath);
        }
        finally
        {
            File.Delete(environmentFilePath);
        }
    }

    [Fact]
    public void Save_UsesTheDefaultModelWhenModelIsMissing()
    {
        var service = new SettingsService(CreateStore(out var storagePath));
        Assert.True(service.Save("test-key", null));
        File.Delete(storagePath);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Save_RejectsRecentMessageLimitsOutsideTheSupportedRange(int maxRecentMessages)
    {
        var service = new SettingsService(CreateStore(out var storagePath));

        var saved = service.Save("sk-test", "gpt-test", maxRecentMessages: maxRecentMessages);

        Assert.False(saved);
        File.Delete(storagePath);
    }

    [Theory]
    [InlineData("Short")]
    [InlineData("Balanced")]
    [InlineData("Advanced")]
    public void Save_AcceptsSupportedAnswerLengths(string answerLength)
    {
        var service = new SettingsService(CreateStore(out var storagePath));

        var saved = service.Save("sk-test", "gpt-test", answerLength: answerLength);

        Assert.True(saved);
        File.Delete(storagePath);
    }

    [Fact]
    public void Save_RejectsTheRetiredLargeAnswerLength()
    {
        var service = new SettingsService(CreateStore(out var storagePath));

        var saved = service.Save("sk-test", "gpt-test", answerLength: "Large");

        Assert.False(saved);
        File.Delete(storagePath);
    }
}
