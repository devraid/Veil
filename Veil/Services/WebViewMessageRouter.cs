using System.Text.Json;
using System.Text.Json.Serialization;

namespace Veil.Services;

public sealed class WebViewMessageRouter
{
    private readonly Func<Task> _waitForInitialization;
    private readonly Func<Task> _chatReady;
    private readonly Func<ChatOpenCommand, Task> _chatOpen;
    private readonly Func<Task> _chatNew;
    private readonly Func<ChatDeleteCommand, Task> _chatDelete;
    private readonly Func<ChatRenameCommand, Task> _chatRename;
    private readonly Func<ChatSubmitCommand, Task> _chatSubmit;
    private readonly Func<SaveApiKeyCommand, Task> _settingsSaveApiKey;

    public WebViewMessageRouter(
        Func<Task> waitForInitialization,
        Func<Task> chatReady,
        Func<ChatOpenCommand, Task> chatOpen,
        Func<Task> chatNew,
        Func<ChatDeleteCommand, Task> chatDelete,
        Func<ChatRenameCommand, Task> chatRename,
        Func<ChatSubmitCommand, Task> chatSubmit,
        Func<SaveApiKeyCommand, Task> settingsSaveApiKey)
    {
        _waitForInitialization = waitForInitialization;
        _chatReady = chatReady;
        _chatOpen = chatOpen;
        _chatNew = chatNew;
        _chatDelete = chatDelete;
        _chatRename = chatRename;
        _chatSubmit = chatSubmit;
        _settingsSaveApiKey = settingsSaveApiKey;
    }

    public async Task RouteAsync(string webMessageJson)
    {
        using var document = JsonDocument.Parse(webMessageJson);
        var root = document.RootElement;
        if (!root.TryGetProperty("type", out var typeProperty) || typeProperty.GetString() is not { } type)
        {
            return;
        }

        switch (type)
        {
            case "chat.ready":
                await _waitForInitialization();
                await _chatReady();
                break;
            case "chat.open":
                await _waitForInitialization();
                await _chatOpen(Deserialize<ChatOpenCommand>(root));
                break;
            case "chat.new":
                await _waitForInitialization();
                await _chatNew();
                break;
            case "chat.delete":
                await _waitForInitialization();
                await _chatDelete(Deserialize<ChatDeleteCommand>(root));
                break;
            case "chat.rename":
                await _waitForInitialization();
                await _chatRename(Deserialize<ChatRenameCommand>(root));
                break;
            case "chat.submit":
                await _waitForInitialization();
                await _chatSubmit(Deserialize<ChatSubmitCommand>(root));
                break;
            case "settings.saveApiKey":
                await _settingsSaveApiKey(Deserialize<SaveApiKeyCommand>(root));
                break;
        }
    }

    private static T Deserialize<T>(JsonElement root) =>
        root.Deserialize<T>(SerializerOptions) ?? throw new JsonException($"Invalid {typeof(T).Name} message.");

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip
    };
}
