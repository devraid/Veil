using System.Diagnostics;

namespace Veil.Tests;

public sealed class PublishSmokeTests
{
    [Fact]
    public void PublishOutput_ContainsOnlyStandaloneExecutable()
    {
        var publishDirectory = GetPublishDirectory();
        Assert.True(Directory.Exists(publishDirectory), $"Publish directory was not found: {publishDirectory}");

        var files = Directory.GetFiles(publishDirectory, "*", SearchOption.TopDirectoryOnly);
        var executable = Assert.Single(files, path => string.Equals(Path.GetFileName(path), "Veil.exe", StringComparison.OrdinalIgnoreCase));
        Assert.True(new FileInfo(executable).Length > 10_000_000, "Standalone executable is unexpectedly small.");
    }

    [Fact]
    public void PublishedExecutable_HasWindowsExecutableHeader()
    {
        var publishDirectory = GetPublishDirectory();
        var executable = Path.Combine(publishDirectory, "Veil.exe");
        Assert.True(File.Exists(executable), $"Published executable was not found: {executable}");

        using var stream = File.OpenRead(executable);
        Assert.Equal((byte)'M', stream.ReadByte());
        Assert.Equal((byte)'Z', stream.ReadByte());
    }

    private static string GetPublishDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Veil.slnx")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory?.FullName ?? throw new InvalidOperationException("Solution root was not found."), "publish", "win-x64");
    }
}
