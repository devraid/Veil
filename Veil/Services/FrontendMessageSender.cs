using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace Veil.Services;

public sealed class FrontendMessageSender
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task SendAsync(CoreWebView2 webView, object message)
    {
        var json = JsonSerializer.Serialize(message, SerializerOptions);
        var encodedJson = JsonSerializer.Serialize(json);
        await webView.ExecuteScriptAsync($"window.veilChat?.receive(JSON.parse({encodedJson}));");
    }
}
