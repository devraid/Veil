using Veil.Services;

namespace Veil.Tests;

public sealed class ImageStorageServiceTests
{
    [Fact]
    public async Task SaveDataUrlAsync_SavesSupportedImageAndReturnsPath()
    {
        var service = new ImageStorageService();
        var path = await service.SaveDataUrlAsync("data:image/png;base64," + Convert.ToBase64String([1, 2, 3]));

        try
        {
            Assert.NotNull(path);
            Assert.EndsWith(".png", path, StringComparison.OrdinalIgnoreCase);
            Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(path!));
        }
        finally
        {
            if (path is not null && File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-image")]
    [InlineData("data:image/png;base64,invalid")]
    [InlineData("data:image/bmp;base64,AA==")]
    public async Task SaveDataUrlAsync_RejectsUnsupportedData(string? dataUrl)
    {
        var service = new ImageStorageService();
        Assert.Null(await service.SaveDataUrlAsync(dataUrl));
    }
}
