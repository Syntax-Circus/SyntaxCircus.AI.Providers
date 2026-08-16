using System.Net;
using System.Net.Http.Json;
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
    public async Task<AiCompletionResult> SendAsync(
        string prompt,
        string? systemPrompt = null,
        IReadOnlyList<AiChatMessage>? conversationHistory = null,
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

        var body = new AnthropicRequest(opts.Model, opts.MaxTokens, systemPrompt, messages);

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
        List<AnthropicMessage> Messages);

    private sealed record AnthropicMessage(string Role, string Content);

    private sealed record AnthropicResponse(List<AnthropicContentBlock>? Content, AnthropicUsage? Usage);

    private sealed record AnthropicContentBlock(string? Text);

    private sealed record AnthropicUsage([property: JsonPropertyName("output_tokens")] int? OutputTokens);
}
