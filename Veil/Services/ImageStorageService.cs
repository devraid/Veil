using System.IO;

namespace Veil.Services;

public sealed class ImageStorageService
{
    private static readonly string ImageDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Veil",
        "Images");

    public async Task<string?> SaveDataUrlAsync(string? imageDataUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imageDataUrl) ||
            !imageDataUrl.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var separatorIndex = imageDataUrl.IndexOf(',', StringComparison.Ordinal);
        if (separatorIndex < 0 || GetImageExtension(imageDataUrl[..separatorIndex]) is not { } extension)
        {
            return null;
        }

        byte[] imageBytes;
        try
        {
            imageBytes = Convert.FromBase64String(imageDataUrl[(separatorIndex + 1)..]);
        }
        catch (FormatException)
        {
            return null;
        }

        Directory.CreateDirectory(ImageDirectory);
        var imagePath = Path.Combine(ImageDirectory, $"{Guid.NewGuid():N}{extension}");
        await File.WriteAllBytesAsync(imagePath, imageBytes, cancellationToken);
        return imagePath;
    }

    private static string? GetImageExtension(string header) => header.ToLowerInvariant() switch
    {
        "data:image/jpeg;base64" => ".jpg",
        "data:image/png;base64" => ".png",
        "data:image/gif;base64" => ".gif",
        "data:image/webp;base64" => ".webp",
        _ => null
    };
}
