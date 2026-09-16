using Veil.Services;

namespace Veil.Tests;

public sealed class ChatRequestCancellationTests
{
    [Fact]
    public async Task StartNext_CancelsPreviousRequest()
    {
        using var cancellation = new ChatRequestCancellation();
        var first = cancellation.StartNext();
        var second = cancellation.StartNext();

        Assert.True(first.IsCancellationRequested);
        Assert.False(second.IsCancellationRequested);
        await Task.CompletedTask;
    }

    [Fact]
    public void Dispose_CancelsActiveRequest()
    {
        var cancellation = new ChatRequestCancellation();
        var token = cancellation.StartNext();

        cancellation.Dispose();

        Assert.True(token.IsCancellationRequested);
    }
}