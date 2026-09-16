using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Web.WebView2.Core;
using Veil.Services;

namespace Veil.Tests;

public sealed class ChatRequestServiceTests
{
    [Fact]
    public void Service_IsDisposableAndCanBeConstructedWithInjectedDependencies()
    {
        using var service = new ChatRequestService(
            new StartupService(
                new OpenAiChatService(new SettingsService(new AppSettingsStore()), new ImageDataUrlService()),
                new ImageDataUrlService()),
            new ImageStorageService(),
            new FrontendMessageSender(),
            new ChatRequestCancellation(),
            NullLogger<ChatRequestService>.Instance);

        Assert.NotNull(service);
    }
}
