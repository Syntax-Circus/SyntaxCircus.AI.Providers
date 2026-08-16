using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace SyntaxCircus.AI.Providers;

/// <summary>
/// Thin wrapper over the Gemini <c>generateContent</c> API. Sends the API key via the
/// <c>x-goog-api-key</c> header rather than the <c>?key=</c> query-string parameter some sample
/// code uses — the query string ends up in server logs, proxy logs, and the Referer header of
/// any request the response triggers, which is a real key-leak vector.
/// </summary>
public sealed class GeminiClient(HttpClient httpClient, IOptions<GeminiClientOptions> options)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Sends <paramref name="prompt"/> (with any <paramref name="conversationHistory"/> folded
    /// into a single turn — Gemini's <c>contents</c> array models multi-turn chat differently
    /// than Anthropic's, so history here is flattened into a labeled text block rather than
    /// mapped 1:1) and returns the model's reply.
    /// </summary>
    /// <param name="responseJsonSchema">
    /// Optional raw JSON Schema string. When set, the request asks Gemini to constrain its
    /// output to this schema and return it as <c>application/json</c>.
    /// </param>
    public async Task<AiCompletionResult> SendAsync(
        string prompt,
        string? systemPrompt = null,
        IReadOnlyList<AiChatMessage>? conversationHistory = null,
        string? responseJsonSchema = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.ApiKey))
        {
            return new AiCompletionResult(string.Empty, Error: "Gemini API key is not configured.");
        }

        JsonElement? schema = string.IsNullOrWhiteSpace(responseJsonSchema)
            ? null
            : JsonSerializer.Deserialize<JsonElement>(responseJsonSchema);

        var body = new GeminiGenerateRequest(
            [new GeminiContent("user", [new GeminiPart(BuildPrompt(prompt, conversationHistory))])],
            string.IsNullOrWhiteSpace(systemPrompt) ? null : new GeminiSystemInstruction([new GeminiPart(systemPrompt)]),
            new GeminiGenerationConfig(opts.MaxOutputTokens, schema is not null ? "application/json" : null, schema));

        using var request = new HttpRequestMessage(HttpMethod.Post, $"v1beta/models/{opts.Model}:generateContent")
        {
            Content = JsonContent.Create(body, options: JsonOptions),
        };
        request.Headers.Add("x-goog-api-key", opts.ApiKey);

        try
        {
            using var response = await httpClient.SendAsync(request, ct).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                return new AiCompletionResult(string.Empty, Error: "Invalid request.");
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                return new AiCompletionResult(string.Empty, Error: "Invalid Gemini API key.");
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                return new AiCompletionResult(string.Empty, Error: "Rate limit exceeded.", IsRateLimited: true, RetryAfter: RetryAfterParser.Parse(response));
            }

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<GeminiGenerateResponse>(JsonOptions, ct).ConfigureAwait(false);
            var candidate = result?.Candidates?.FirstOrDefault();
            var text = candidate?.Content?.Parts?.FirstOrDefault()?.Text;

            if (candidate is null || string.IsNullOrWhiteSpace(text))
            {
                return new AiCompletionResult(string.Empty, Error: "Content filtered by safety settings.");
            }

            return new AiCompletionResult(text, result?.UsageMetadata?.TotalTokenCount);
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
            return new AiCompletionResult(string.Empty, Error: "Malformed response from Gemini API.");
        }
    }

    private static string BuildPrompt(string prompt, IReadOnlyList<AiChatMessage>? history)
    {
        if (history is null || history.Count == 0)
        {
            return prompt;
        }

        var parts = new List<string>();
        foreach (var message in history)
        {
            parts.Add($"[{message.Role}]\n{message.Content}\n");
        }

        parts.Add(prompt);
        return string.Join("\n", parts);
    }

    private sealed record GeminiGenerateRequest(List<GeminiContent> Contents, GeminiSystemInstruction? SystemInstruction, GeminiGenerationConfig GenerationConfig);

    private sealed record GeminiContent(string Role, List<GeminiPart> Parts);

    private sealed record GeminiPart(string Text);

    private sealed record GeminiSystemInstruction(List<GeminiPart> Parts);

    private sealed record GeminiGenerationConfig(int MaxOutputTokens, string? ResponseMimeType, JsonElement? ResponseSchema);

    private sealed record GeminiGenerateResponse(List<GeminiCandidate>? Candidates, GeminiUsageMetadata? UsageMetadata);

    private sealed record GeminiCandidate(GeminiCandidateContent? Content);

    private sealed record GeminiCandidateContent(List<GeminiPart>? Parts);

    private sealed record GeminiUsageMetadata([property: JsonPropertyName("totalTokenCount")] int? TotalTokenCount);
}
