using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Veil.Services;

public sealed class OpenAiChatService
{
    private const string DefaultModel = "gpt-4o-mini";
    private static readonly HttpClient HttpClient = new();
    private readonly AppSettingsStore _appSettingsStore = new();

    public async Task<string> GenerateResponseAsync(
        IReadOnlyList<ChatTurn> conversation,
        CancellationToken cancellationToken = default)
    {
        var settings = _appSettingsStore.GetStoredSettings();
        var apiKey = settings?.ApiKey ?? GetEnvironmentValue("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("OPENAI_API_KEY is not configured.");
        }

        var model = string.IsNullOrWhiteSpace(settings?.Model)
            ? GetEnvironmentValue("OPENAI_MODEL") ?? DefaultModel
            : settings.Model;

        var request = new
        {
            model,
            messages = conversation.Select(CreateOpenAiMessage)
        };

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "https://api.openai.com/v1/chat/completions")
        {
            Content = JsonContent.Create(request)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await HttpClient.SendAsync(httpRequest, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"OpenAI request failed with status {(int)response.StatusCode}: {GetErrorMessage(responseBody)}");
        }

        using var document = JsonDocument.Parse(responseBody);
        var content = document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("OpenAI returned an empty response.");
        }

        return content.Trim();
    }

    private static object CreateOpenAiMessage(ChatTurn turn)
    {
        var content = new List<object>();
        if (!string.IsNullOrWhiteSpace(turn.Content))
        {
            content.Add(new { type = "text", text = turn.Content });
        }

        var imageDataUrl = ReadImageAsDataUrl(turn.Image);
        if (imageDataUrl is not null)
        {
            content.Add(new
            {
                type = "image_url",
                image_url = new { url = imageDataUrl }
            });
        }

        return new
        {
            role = turn.Role == "ai" ? "assistant" : turn.Role,
            content
        };
    }

    private static string? ReadImageAsDataUrl(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
        {
            return null;
        }

        var mimeType = Path.GetExtension(imagePath).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => null
        };

        return mimeType is null
            ? null
            : $"data:{mimeType};base64,{Convert.ToBase64String(File.ReadAllBytes(imagePath))}";
    }

    private static string? GetEnvironmentValue(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value.Trim();
        }

        var path = Path.Combine(AppContext.BaseDirectory, ".env");
        if (!File.Exists(path))
        {
            return null;
        }

        foreach (var line in File.ReadLines(path))
        {
            var trimmedLine = line.Trim();
            if (trimmedLine.Length == 0 || trimmedLine.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = trimmedLine.IndexOf('=');
            if (separatorIndex <= 0 ||
                !string.Equals(trimmedLine[..separatorIndex].Trim(), name, StringComparison.Ordinal))
            {
                continue;
            }

            return trimmedLine[(separatorIndex + 1)..].Trim().Trim('"', '\'');
        }

        return null;
    }

    private static string GetErrorMessage(string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            return document.RootElement
                .GetProperty("error")
                .GetProperty("message")
                .GetString() ?? "Unknown OpenAI error.";
        }
        catch (JsonException)
        {
            return "Unknown OpenAI error.";
        }
    }
}

public sealed record ChatTurn(string Role, string Content, string? Image);
