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
    /// Optional raw JSON Schema string. When set, the request asks Anthropic to constrain its
    /// output to this schema and return it as valid JSON conforming to the schema.
    /// </param>
    /// <param name="skipSchemaValidation">
    /// If true, skips client-side schema validation. Only use when you have a non-standard
    /// schema format or want to let the API reject invalid schemas.
    /// </param>
    public async Task<AiCompletionResult> SendAsync(
        string prompt,
        string? systemPrompt = null,
        IReadOnlyList<AiChatMessage>? conversationHistory = null,
        string? responseJsonSchema = null,
        bool skipSchemaValidation = false,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.ApiKey))
        {
            return new AiCompletionResult(string.Empty, Error: "Anthropic API key is not configured.");
        }

        if (!string.IsNullOrWhiteSpace(responseJsonSchema) && !skipSchemaValidation)
        {
            var schemaValidation = SchemaValidator.Validate(responseJsonSchema, validateStructure: true);
            if (!schemaValidation.IsValid)
            {
                return new AiCompletionResult(string.Empty, Error: $"Invalid schema: {schemaValidation.Error}");
            }
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

        AnthropicOutputConfig? outputConfig = null;
        if (!string.IsNullOrWhiteSpace(responseJsonSchema))
        {
            var schema = JsonSerializer.Deserialize<JsonElement>(responseJsonSchema);
            outputConfig = new AnthropicOutputConfig(
                new AnthropicOutputFormat("json_schema", schema));
        }

        var body = new AnthropicRequest(opts.Model, opts.MaxTokens, systemPrompt, messages, outputConfig);

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

            var responseText = result.Content[0].Text ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(responseJsonSchema))
            {
                var validationResult = ValidateResponseSchema(responseText, responseJsonSchema);
                if (!validationResult.IsValid)
                {
                    return new AiCompletionResult(string.Empty, Error: $"Response does not conform to schema: {validationResult.Error}");
                }
            }

            return new AiCompletionResult(responseText, TokensUsed: result.Usage?.OutputTokens);
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
        [property: JsonPropertyName("output_config")] AnthropicOutputConfig? OutputConfig = null);

    private sealed record AnthropicMessage(string Role, string Content);

    private sealed record AnthropicOutputConfig(
        [property: JsonPropertyName("format")] AnthropicOutputFormat Format);

    private sealed record AnthropicOutputFormat(
        string Type,
        JsonElement? Schema = null);

    private sealed record AnthropicResponse(List<AnthropicContentBlock>? Content, AnthropicUsage? Usage);

    private sealed record AnthropicContentBlock(string? Text);

    private sealed record AnthropicUsage([property: JsonPropertyName("output_tokens")] int? OutputTokens);

    /// <summary>
    /// Validates that a response conforms to the provided JSON schema.
    /// </summary>
    private static SchemaValidationResult ValidateResponseSchema(string response, string schema)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return new SchemaValidationResult(IsValid: false, Error: "Response is empty.");
        }

        try
        {
            using var responseDoc = JsonDocument.Parse(response);
            var responseElement = responseDoc.RootElement;

            using var schemaDoc = JsonDocument.Parse(schema);
            var schemaElement = schemaDoc.RootElement;

            if (!schemaElement.TryGetProperty("type", out var typeElement))
            {
                return new SchemaValidationResult(IsValid: true);
            }

            var expectedType = typeElement.GetString();
            var actualType = responseElement.ValueKind switch
            {
                JsonValueKind.Object => "object",
                JsonValueKind.Array => "array",
                JsonValueKind.String => "string",
                JsonValueKind.Number => "number",
                JsonValueKind.True or JsonValueKind.False => "boolean",
                JsonValueKind.Null => "null",
                _ => "unknown",
            };

            if (expectedType != actualType)
            {
                return new SchemaValidationResult(
                    IsValid: false,
                    Error: $"Response type '{actualType}' does not match schema type '{expectedType}'.");
            }

            return new SchemaValidationResult(IsValid: true);
        }
        catch (JsonException ex)
        {
            return new SchemaValidationResult(IsValid: false, Error: $"Response is not valid JSON: {ex.Message}");
        }
    }
}
