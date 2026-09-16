using System.Text.Json.Serialization;

namespace Veil.Services;

public sealed record ChatsLoadedResponse(IReadOnlyList<ChatService.ChatSummary> Chats, string Type = "chats.loaded");
public sealed record ChatLoadedResponse(Guid ChatId, IReadOnlyList<ChatEntryResponse> Entries, string Type = "chat.loaded");
public sealed record ChatAddedResponse(ChatEntryResponse Entry, string Type = "chat.added");
public sealed record ChatErrorResponse(string Message, string Type = "chat.error");
public sealed record ApiKeyStatusResponse(bool Configured, string? Model, string Type = "settings.apiKeyStatus");
public sealed record ApiKeySavedResponse(bool Success, string? Message = null, string Type = "settings.apiKeySaved");

public sealed record ChatOpenCommand(Guid ChatId)
{
    [JsonIgnore]
    public string Type => "chat.open";
}

public sealed record ChatDeleteCommand(Guid ChatId)
{
    [JsonIgnore]
    public string Type => "chat.delete";
}

public sealed record ChatRenameCommand(Guid ChatId, string Title)
{
    [JsonIgnore]
    public string Type => "chat.rename";
}

public sealed record ChatSubmitCommand(string? Text, string? Image)
{
    [JsonIgnore]
    public string Type => "chat.submit";
}

public sealed record SaveApiKeyCommand(string? ApiKey, string? Model)
{
    [JsonIgnore]
    public string Type => "settings.saveApiKey";
}

public sealed record ChatEntryResponse(
    int Id,
    string Role,
    string? UserText,
    string Answer,
    DateTime Timestamp,
    string? Image);
