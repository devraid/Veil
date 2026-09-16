using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Web.WebView2.Core;
using Veil.Services;

namespace Veil.Tests;

public sealed class ChatRequestServiceTests
{
    [Fact]
    public void Service_IsDisposableAndCanBeConstructedWithInjectedDependencies()
    {
        var settingsService = new SettingsService(new AppSettingsStore());
        using var service = new ChatRequestService(
            new StartupService(
                new OpenAiChatService(settingsService, new ImageDataUrlService()),
                new ImageDataUrlService(),
                settingsService),
            new ImageStorageService(),
            new FrontendMessageSender(),
            new ChatRequestCancellation(),
            NullLogger<ChatRequestService>.Instance);

        Assert.NotNull(service);
    }
}
