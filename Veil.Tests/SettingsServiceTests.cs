using Veil.Services;

namespace Veil.Tests;

public sealed class SettingsServiceTests
{
    [Fact]
    public void Save_RejectsMissingModel()
    {
        var service = new SettingsService(new AppSettingsStore());
        Assert.False(service.Save("test-key", null));
    }
}
