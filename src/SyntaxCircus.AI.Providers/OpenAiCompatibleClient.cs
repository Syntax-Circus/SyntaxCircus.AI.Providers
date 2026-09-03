using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace SyntaxCircus.AI.Providers;

/// <summary>
/// Thin wrapper over the OpenAI-compatible <c>chat/completions</c> API shape shared by OpenAI
/// itself and the many gateways/self-hosted servers that mimic it (e.g. OpenRouter). Unlike
/// <see cref="AnthropicClient"/> and <see cref="GeminiClient"/>, the host is not fixed for this
/// provider family — a caller-supplied <c>baseUrlOverride</c> lets it target whichever
/// OpenAI-compatible endpoint is configured at runtime rather than a single well-known host.
/// Sends the API key via a standard <c>Authorization: Bearer</c> header, per OpenAI-compatible
/// convention — distinct from Anthropic's <c>x-api-key</c> and Gemini's <c>x-goog-api-key</c>.
/// </summary>
public sealed class OpenAiCompatibleClient(HttpClient httpClient, IOptions<OpenAiCompatibleClientOptions> options)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Sends <paramref name="prompt"/> (with any <paramref name="conversationHistory"/> mapped
    /// directly into the <c>messages</c> array, ahead of the final user turn) and returns the
    /// model's reply.
    /// </summary>
    public Task<AiCompletionResult> SendAsync(
        string prompt,
        string? systemPrompt = null,
        IReadOnlyList<AiChatMessage>? conversationHistory = null,
        string? responseJsonSchema = null,
        CancellationToken ct = default)
        => SendAsync(prompt, apiKeyOverride: null, systemPrompt, conversationHistory, responseJsonSchema, modelOverride: null, baseUrlOverride: null, ct);

    /// <summary>Sends a request using a caller-supplied API key instead of the configured key.</summary>
    public Task<AiCompletionResult> SendAsync(
        string prompt,
        string? apiKeyOverride,
        string? systemPrompt = null,
        IReadOnlyList<AiChatMessage>? conversationHistory = null,
        string? responseJsonSchema = null,
        CancellationToken ct = default)
        => SendAsync(prompt, apiKeyOverride, systemPrompt, conversationHistory, responseJsonSchema, modelOverride: null, baseUrlOverride: null, ct);

    /// <summary>
    /// Sends a request using a caller-supplied API key and/or model instead of the configured
    /// values. <paramref name="modelOverride"/> is useful when the model is a per-user or
    /// per-request setting (e.g. stored in application preferences) rather than a fixed,
    /// process-wide configuration value.
    /// </summary>
    public Task<AiCompletionResult> SendAsync(
        string prompt,
        string? apiKeyOverride,
        string? systemPrompt,
        IReadOnlyList<AiChatMessage>? conversationHistory,
        string? responseJsonSchema,
        string? modelOverride,
        CancellationToken ct = default)
        => SendAsync(prompt, apiKeyOverride, systemPrompt, conversationHistory, responseJsonSchema, modelOverride, baseUrlOverride: null, ct);

    /// <summary>
    /// Sends a request using a caller-supplied API key, model, and/or base URL instead of the
    /// configured values. <paramref name="baseUrlOverride"/> is the reason this client exists
    /// alongside <see cref="AnthropicClient"/> / <see cref="GeminiClient"/>: OpenAI-compatible
    /// endpoints (self-hosted gateways, OpenRouter, etc.) commonly have a per-tenant or
    /// per-request host rather than a single fixed one, so the host is resolved per call instead
    /// of being baked into the injected <see cref="HttpClient"/>'s <c>BaseAddress</c>.
    /// </summary>
    public async Task<AiCompletionResult> SendAsync(
        string prompt,
        string? apiKeyOverride,
        string? systemPrompt,
        IReadOnlyList<AiChatMessage>? conversationHistory,
        string? responseJsonSchema,
        string? modelOverride,
        string? baseUrlOverride,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        var opts = options.Value;
        var apiKey = string.IsNullOrWhiteSpace(apiKeyOverride) ? opts.ApiKey : apiKeyOverride;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new AiCompletionResult(string.Empty, Error: "OpenAI-compatible API key is not configured.");
        }

        var baseUrl = string.IsNullOrWhiteSpace(baseUrlOverride) ? opts.BaseUrl : baseUrlOverride;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return new AiCompletionResult(string.Empty, Error: "OpenAI-compatible base URL is not configured.");
        }

        var model = string.IsNullOrWhiteSpace(modelOverride) ? opts.Model : modelOverride;

        var messages = new List<OpenAiChatMessage>();
        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            messages.Add(new OpenAiChatMessage("system", systemPrompt));
        }

        if (conversationHistory is not null)
        {
            foreach (var message in conversationHistory)
            {
                messages.Add(new OpenAiChatMessage(message.Role, message.Content));
            }
        }

        messages.Add(new OpenAiChatMessage("user", prompt));

        var body = new OpenAiChatRequest(model, messages, opts.MaxTokens);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/chat/completions")
        {
            Content = JsonContent.Create(body, options: JsonOptions),
        };
        request.Headers.Add("Authorization", $"Bearer {apiKey}");

        try
        {
            using var response = await httpClient.SendAsync(request, ct).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                return new AiCompletionResult(string.Empty, Error: "Invalid request.");
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
            {
                return new AiCompletionResult(string.Empty, Error: "Invalid API key.");
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                return new AiCompletionResult(string.Empty, Error: "Rate limit exceeded.", IsRateLimited: true, RetryAfter: RetryAfterParser.Parse(response));
            }

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OpenAiChatResponse>(JsonOptions, ct).ConfigureAwait(false);
            var content = result?.Choices?.FirstOrDefault()?.Message?.Content;

            if (string.IsNullOrWhiteSpace(content))
            {
                return new AiCompletionResult(string.Empty, Error: "Empty response from provider.");
            }

            return new AiCompletionResult(content, result?.Usage?.TotalTokens);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return new AiCompletionResult(string.Empty, Error: "Request timed out.");
        }
        catch (HttpRequestException ex)
        {
            return new AiCompletionResult(string.Empty, Error: $"HTTP error: {ex.Message}");
        }
        catch (JsonException)
        {
            return new AiCompletionResult(string.Empty, Error: "Malformed response from provider.");
        }
    }

    private sealed record OpenAiChatRequest(string Model, List<OpenAiChatMessage> Messages, [property: JsonPropertyName("max_tokens")] int MaxTokens);

    private sealed record OpenAiChatMessage(string Role, string Content);

    private sealed record OpenAiChatResponse(List<OpenAiChatChoice>? Choices, OpenAiChatUsage? Usage);

    private sealed record OpenAiChatChoice(OpenAiChatResponseMessage? Message);

    private sealed record OpenAiChatResponseMessage(string? Content);

    private sealed record OpenAiChatUsage([property: JsonPropertyName("total_tokens")] int? TotalTokens);
}
