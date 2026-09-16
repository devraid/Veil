using Veil.Services;

namespace Veil.Tests;

public sealed class WebViewMessageRouterTests
{
    [Fact]
    public async Task ChatSubmit_IsDeserializedIntoTypedCommand()
    {
        ChatSubmitCommand? received = null;
        var router = CreateRouter(chatSubmit: command =>
        {
            received = command;
            return Task.CompletedTask;
        });

        await router.RouteAsync("{\"type\":\"chat.submit\",\"text\":\" hello \",\"image\":null}");

        Assert.NotNull(received);
        Assert.Equal(" hello ", received!.Text);
    }

    [Fact]
    public async Task ChatReady_WaitsForInitializationBeforeLoadingChats()
    {
        var events = new List<string>();
        var router = CreateRouter(
            waitForInitialization: () => { events.Add("initialized"); return Task.CompletedTask; },
            chatReady: () => { events.Add("ready"); return Task.CompletedTask; });

        await router.RouteAsync("{\"type\":\"chat.ready\"}");

        Assert.Equal(new[] { "initialized", "ready" }, events);
    }

    [Fact]
    public async Task ChatList_WaitsForInitializationBeforeLoadingChats()
    {
        var events = new List<string>();
        var router = CreateRouter(
            waitForInitialization: () => { events.Add("initialized"); return Task.CompletedTask; },
            chatList: () => { events.Add("list"); return Task.CompletedTask; });

        await router.RouteAsync("{\"type\":\"chat.list\"}");

        Assert.Equal(new[] { "initialized", "list" }, events);
    }

    private static WebViewMessageRouter CreateRouter(
        Func<Task>? waitForInitialization = null,
        Func<Task>? chatReady = null,
        Func<Task>? chatList = null,
        Func<ChatSubmitCommand, Task>? chatSubmit = null) => new(
        waitForInitialization ?? (() => Task.CompletedTask),
        chatReady ?? (() => Task.CompletedTask),
        chatList ?? (() => Task.CompletedTask),
        _ => Task.CompletedTask,
        () => Task.CompletedTask,
        _ => Task.CompletedTask,
        _ => Task.CompletedTask,
        chatSubmit ?? (_ => Task.CompletedTask),
        _ => Task.CompletedTask);
}