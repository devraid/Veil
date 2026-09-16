using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Veil.Services;

public sealed class OpenAiChatService
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(60);
    private static readonly HttpClient HttpClient = new();
    private readonly SettingsService _settingsService;
    private readonly ImageDataUrlService _imageDataUrlService;

    public OpenAiChatService(SettingsService settingsService, ImageDataUrlService imageDataUrlService)
    {
        _settingsService = settingsService;
        _imageDataUrlService = imageDataUrlService;
    }

    public async Task<string> GenerateResponseAsync(
        IReadOnlyList<ChatTurn> conversation,
        CancellationToken cancellationToken = default)
    {
        var settings = _settingsService.GetEffectiveSettings();
        var apiKey = settings.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("OPENAI_API_KEY is not configured.");
        }

        var model = string.IsNullOrWhiteSpace(settings.Model) ? AppSettingsStore.DefaultModel : settings.Model;

        var request = new
        {
            model,
            messages = conversation.Select(CreateOpenAiMessage),
            max_completion_tokens = GetMaxCompletionTokens(settings.AnswerLength)
        };

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "https://api.openai.com/v1/chat/completions")
        {
            Content = JsonContent.Create(request)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(RequestTimeout);
        using var response = await HttpClient.SendAsync(httpRequest, timeoutSource.Token);
        var responseBody = await response.Content.ReadAsStringAsync(timeoutSource.Token);
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

    public async Task<string> GenerateSummaryAsync(
        string? existingSummary,
        IReadOnlyList<ChatTurn> messages,
        CancellationToken cancellationToken = default)
    {
        var summaryPrompt = "Maintain a short internal conversation summary. Keep goals, decisions, facts, preferences, and unresolved work. Omit small talk and repetition.";
        var summaryContext = new List<ChatTurn> { new("system", summaryPrompt, null) };
        if (!string.IsNullOrWhiteSpace(existingSummary))
        {
            summaryContext.Add(new ChatTurn("system", $"Existing summary:\n{existingSummary}", null));
        }
        summaryContext.AddRange(messages);
        return await GenerateCompletionAsync(summaryContext, 500, cancellationToken);
    }

    private async Task<string> GenerateCompletionAsync(
        IReadOnlyList<ChatTurn> conversation,
        int maxCompletionTokens,
        CancellationToken cancellationToken)
    {
        var settings = _settingsService.GetEffectiveSettings();
        var apiKey = settings.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("OPENAI_API_KEY is not configured.");
        }

        var model = string.IsNullOrWhiteSpace(settings.Model) ? AppSettingsStore.DefaultModel : settings.Model;
        var request = new { model, messages = conversation.Select(CreateOpenAiMessage), max_completion_tokens = maxCompletionTokens };
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
        {
            Content = JsonContent.Create(request)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(RequestTimeout);
        using var response = await HttpClient.SendAsync(httpRequest, timeoutSource.Token);
        var responseBody = await response.Content.ReadAsStringAsync(timeoutSource.Token);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"OpenAI request failed with status {(int)response.StatusCode}: {GetErrorMessage(responseBody)}");
        }
        using var document = JsonDocument.Parse(responseBody);
        return document.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()?.Trim()
            ?? throw new InvalidOperationException("OpenAI returned an empty response.");
    }

    private static int GetMaxCompletionTokens(string? answerLength) => answerLength switch
    {
        "Short" => 300,
        "Advanced" or "Large" => 2_000,
        _ => 800
    };

    private object CreateOpenAiMessage(ChatTurn turn)
    {
        var content = new List<object>();
        if (!string.IsNullOrWhiteSpace(turn.Content))
        {
            content.Add(new { type = "text", text = turn.Content });
        }

        var imageDataUrl = _imageDataUrlService.ReadAsDataUrl(turn.Image);
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
