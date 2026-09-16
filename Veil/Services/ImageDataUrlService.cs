using System;
using System.IO;

namespace Veil.Services;

public sealed class ImageDataUrlService
{
    public string? ReadAsDataUrl(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
        {
            return null;
        }

        var mimeType = GetMimeType(Path.GetExtension(imagePath));
        return mimeType is null
            ? null
            : $"data:{mimeType};base64,{Convert.ToBase64String(File.ReadAllBytes(imagePath))}";
    }

    private static string? GetMimeType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => null
        };
    }
}
