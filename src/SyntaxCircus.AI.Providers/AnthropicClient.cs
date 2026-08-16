using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SyntaxCircus.AI.Providers;

/// <summary>Thin wrapper over the Anthropic Messages API (<c>POST /v1/messages</c>).</summary>
public sealed class AnthropicClient(HttpClient httpClient, IOptions<AnthropicClientOptions> options)
{
    private const string AnthropicVersion = "2023-06-01";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Sends <paramref name="prompt"/> (appended after any <paramref name="conversationHistory"/>)
    /// and returns the model's reply. API-level failures (missing key, 401, 429, 5xx) come back
    /// as a non-<see cref="AiCompletionResult.Success"/> result rather than a thrown exception.
    /// </summary>
    /// <param name="responseJsonSchema">
    /// Optional raw JSON Schema string. When set, the request asks Anthropic to emit a structured
    /// tool use response that matches the schema and can be parsed as JSON.
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
            return new AiCompletionResult(string.Empty, Error: "Anthropic API key is not configured.");
        }

        var messages = new List<AnthropicMessage>();
        if (conversationHistory is not null)
        {
            foreach (var message in conversationHistory)
            {
                messages.Add(new AnthropicMessage(message.Role, message.Content));
            }
        }

        messages.Add(new AnthropicMessage("user", prompt));

        JsonElement? schema = string.IsNullOrWhiteSpace(responseJsonSchema)
            ? null
            : JsonSerializer.Deserialize<JsonElement>(responseJsonSchema);

        var body = new AnthropicRequest(
            opts.Model,
            opts.MaxTokens,
            systemPrompt,
            messages,
            schema is null
                ? null
                : [new AnthropicTool("structured_output", "Return the response as structured JSON.", schema.Value)],
            schema is null ? null : new AnthropicToolChoice("tool", "structured_output"));

        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/messages")
        {
            Content = JsonContent.Create(body, options: JsonOptions),
        };
        request.Headers.Add("x-api-key", opts.ApiKey);
        request.Headers.Add("anthropic-version", AnthropicVersion);

        try
        {
            using var response = await httpClient.SendAsync(request, ct).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return new AiCompletionResult(string.Empty, Error: "Invalid Anthropic API key.");
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                return new AiCompletionResult(string.Empty, Error: "Rate limit exceeded.", IsRateLimited: true, RetryAfter: RetryAfterParser.Parse(response));
            }

            if ((int)response.StatusCode >= 500)
            {
                return new AiCompletionResult(string.Empty, Error: $"Anthropic API error ({(int)response.StatusCode}).");
            }

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<AnthropicResponse>(JsonOptions, ct).ConfigureAwait(false);
            if (result?.Content is not { Count: > 0 })
            {
                return new AiCompletionResult(string.Empty, Error: "Empty response from Anthropic API.");
            }

            if (schema is not null)
            {
                var toolUse = result.Content.FirstOrDefault(block => string.Equals(block.Type, "tool_use", StringComparison.Ordinal));
                if (toolUse?.Input is null)
                {
                    return new AiCompletionResult(string.Empty, Error: "Structured response missing tool output from Anthropic API.");
                }

                return new AiCompletionResult(JsonSerializer.Serialize(toolUse.Input.Value), TokensUsed: result.Usage?.OutputTokens);
            }

            return new AiCompletionResult(result.Content[0].Text ?? string.Empty, TokensUsed: result.Usage?.OutputTokens);
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
            return new AiCompletionResult(string.Empty, Error: "Malformed response from Anthropic API.");
        }
    }

    private sealed record AnthropicRequest(
        string Model,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        string? System,
        List<AnthropicMessage> Messages,
        List<AnthropicTool>? Tools = null,
        AnthropicToolChoice? ToolChoice = null);

    private sealed record AnthropicMessage(string Role, string Content);

    private sealed record AnthropicTool(string Name, string Description, JsonElement InputSchema);

    private sealed record AnthropicToolChoice(string Type, string Name);

    private sealed record AnthropicResponse(List<AnthropicContentBlock>? Content, AnthropicUsage? Usage);

    private sealed record AnthropicContentBlock(string? Type, string? Text, JsonElement? Input);

    private sealed record AnthropicUsage([property: JsonPropertyName("output_tokens")] int? OutputTokens);
}
