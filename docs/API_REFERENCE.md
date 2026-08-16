# API Reference

Complete API documentation for SyntaxCircus.AI.Providers.

## Table of Contents
- [AnthropicClient](#anthropicclient)
- [GeminiClient](#geminiclient)
- [AiCompletionResult](#aicompletionresult)
- [AiChatMessage](#aichatmessage)
- [SchemaValidator](#schemavalidator)
- [Configuration](#configuration)

---

## AnthropicClient

Typed HTTP client for the Anthropic Messages API.

### Constructor

```csharp
public AnthropicClient(HttpClient httpClient, IOptions<AnthropicClientOptions> options)
```

Typically injected via dependency injection. See [INTEGRATION_GUIDE.md](INTEGRATION_GUIDE.md) for setup.

### SendAsync

Sends a prompt to Claude and returns the model's response.

```csharp
public async Task<AiCompletionResult> SendAsync(
    string prompt,
    string? systemPrompt = null,
    IReadOnlyList<AiChatMessage>? conversationHistory = null,
    string? responseJsonSchema = null,
    CancellationToken ct = default)
```

#### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `prompt` | `string` | Yes | The user's message or question to send to Claude |
| `systemPrompt` | `string?` | No | System prompt to guide Claude's behavior. If not provided, uses default behavior |
| `conversationHistory` | `IReadOnlyList<AiChatMessage>?` | No | Previous conversation turns to provide context |
| `responseJsonSchema` | `string?` | No | Raw JSON Schema string to constrain output to a specific format |
| `ct` | `CancellationToken` | No | Cancellation token for the async operation. Default: `default` |

#### Returns

`Task<AiCompletionResult>` — See [AiCompletionResult](#aicompletionresult) for details.

#### Remarks

- All API-level failures (401, 429, 5xx, timeouts) return as `AiCompletionResult` errors, not exceptions
- Rate limit errors set `IsRateLimited = true` and `RetryAfter` timestamp
- When `responseJsonSchema` is provided, Anthropic is asked to emit structured tool output and the tool input is returned as JSON text

#### Example

```csharp
var result = await anthropicClient.SendAsync(
    prompt: "What is machine learning?",
    systemPrompt: "Explain concepts simply.");

if (result.Success)
{
    Console.WriteLine(result.Content);
}
else if (result.IsRateLimited)
{
    await Task.Delay(result.RetryAfter.Value - DateTimeOffset.UtcNow);
}
else
{
    Console.WriteLine($"Error: {result.Error}");
}
```

---

## GeminiClient

Typed HTTP client for the Google Gemini API.

### Constructor

```csharp
public GeminiClient(HttpClient httpClient, IOptions<GeminiClientOptions> options)
```

Typically injected via dependency injection. See [INTEGRATION_GUIDE.md](INTEGRATION_GUIDE.md) for setup.

### SendAsync

Sends a prompt to Gemini and returns the model's response.

```csharp
public async Task<AiCompletionResult> SendAsync(
    string prompt,
    string? systemPrompt = null,
    IReadOnlyList<AiChatMessage>? conversationHistory = null,
    string? responseJsonSchema = null,
    CancellationToken ct = default)
```

#### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `prompt` | `string` | Yes | The user's message or question to send to Gemini |
| `systemPrompt` | `string?` | No | System prompt to guide Gemini's behavior |
| `conversationHistory` | `IReadOnlyList<AiChatMessage>?` | No | Previous conversation turns (flattened into single turn internally) |
| `responseJsonSchema` | `string?` | No | Raw JSON Schema string to constrain output to JSON format |
| `ct` | `CancellationToken` | No | Cancellation token for the async operation |

#### Returns

`Task<AiCompletionResult>` — See [AiCompletionResult](#aicompletionresult) for details.

#### Remarks

- Conversation history is flattened into a single turn (Gemini's API differs from Anthropic's)
- Schema-constrained responses are automatically returned as JSON
- Rate limiting and error handling follow the same pattern as AnthropicClient
- No content filtering by default (but Gemini may still filter unsafe content)

#### Example

```csharp
var result = await geminiClient.SendAsync(
    prompt: "Generate a JSON object with name and age fields",
    responseJsonSchema: schema);
```

---

## AiCompletionResult

Result of an AI completion request.

```csharp
public sealed record AiCompletionResult(
    string Content,
    int? TokensUsed = null,
    string? Error = null,
    bool IsRateLimited = false,
    DateTimeOffset? RetryAfter = null)
{
    public bool Success => Error is null;
}
```

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Content` | `string` | The model's response. Empty if the request failed. |
| `TokensUsed` | `int?` | Number of output tokens used. Null if not provided by provider. |
| `Error` | `string?` | Error message if request failed. Null if successful. |
| `IsRateLimited` | `bool` | True if rate limit (429) was hit. Default: `false` |
| `RetryAfter` | `DateTimeOffset?` | Timestamp when rate limit is lifted. Null if not rate limited. |
| `Success` | `bool` | Convenience property; true if `Error` is null. |

### Usage

```csharp
var result = await client.SendAsync(prompt);

if (result.Success)
{
    Console.WriteLine($"Response: {result.Content}");
    Console.WriteLine($"Tokens: {result.TokensUsed}");
}
else if (result.IsRateLimited)
{
    var waitTime = result.RetryAfter.Value - DateTimeOffset.UtcNow;
    Console.WriteLine($"Rate limited. Retry after {waitTime.TotalSeconds} seconds.");
}
else
{
    Console.WriteLine($"Error: {result.Error}");
}
```

---

## AiChatMessage

Represents a single message in a conversation.

```csharp
public record AiChatMessage(string Role, string Content);
```

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Role` | `string` | Message role: `"user"` or `"assistant"` |
| `Content` | `string` | Message text |

### Usage

```csharp
var history = new List<AiChatMessage>
{
    new("user", "What is the capital of France?"),
    new("assistant", "The capital of France is Paris."),
    new("user", "And what is its population?")
};

var result = await client.SendAsync(
    prompt: "And what is its population?",
    conversationHistory: history);
```

---

## SchemaValidator

Validates JSON schemas for use with structured output.

```csharp
public static class SchemaValidator
{
    public static SchemaValidationResult Validate(
        string schema,
        bool validateStructure = true);
}
```

### Validate Method

Validates that a JSON schema string is well-formed JSON and (optionally) conforms to JSON Schema specification.

#### Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `schema` | `string` | Raw JSON schema string to validate |
| `validateStructure` | `bool` | If true, validates schema structure (requires `"type"` property). Default: `true` |

#### Returns

`SchemaValidationResult` — Contains `IsValid` (bool) and `Error` (string?) properties.

#### Remarks

- Validates JSON well-formedness
- Optionally validates JSON Schema structure (checks for required `"type"` property)
- Returns clear error messages for invalid schemas
- Set `validateStructure = false` to skip structural validation

#### Example

```csharp
var schema = """
{
  "type": "object",
  "properties": { "name": { "type": "string" } }
}
""";

var validation = SchemaValidator.Validate(schema);
if (!validation.IsValid)
{
    Console.WriteLine($"Schema error: {validation.Error}");
}
```

---

## Configuration

### AnthropicClientOptions

Configuration for `AnthropicClient`.

```csharp
public class AnthropicClientOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int MaxTokens { get; set; }
}
```

**Example in appsettings.json**:
```json
{
  "Anthropic": {
    "ApiKey": "sk-ant-...",
    "Model": "claude-opus-5",
    "MaxTokens": 4096
  }
}
```

### GeminiClientOptions

Configuration for `GeminiClient`.

```csharp
public class GeminiClientOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int MaxOutputTokens { get; set; }
}
```

**Example in appsettings.json**:
```json
{
  "Gemini": {
    "ApiKey": "...",
    "Model": "gemini-2.5-flash",
    "MaxOutputTokens": 4096
  }
}
```

---

## Service Registration

### AddAiProviders

Registers both `AnthropicClient` and `GeminiClient` in dependency injection.

```csharp
public static IServiceCollection AddAiProviders(
    this IServiceCollection services,
    IConfiguration configuration)
```

Usage in `Program.cs`:

```csharp
builder.Services.AddAiProviders(builder.Configuration);
```

This registers:
- `AnthropicClient` as a scoped service
- `GeminiClient` as a scoped service
- Binds `"Anthropic"` section to `AnthropicClientOptions`
- Binds `"Gemini"` section to `GeminiClientOptions`

---

## Error Handling

All API-level errors return as `AiCompletionResult` with an `Error` message:

| Scenario | Error Message | IsRateLimited |
|----------|---------------|---------------|
| Missing API key | "API key is not configured." | false |
| Invalid API key (401) | "Invalid API key." | false |
| Rate limit (429) | "Rate limit exceeded." | true |
| Server error (5xx) | "API error (500)." | false |
| Timeout | "Request timed out." | false |
| Invalid schema | "Invalid schema: ..." | false |
| Malformed response | "Malformed response from API." | false |

See [TROUBLESHOOTING.md](TROUBLESHOOTING.md) for handling common errors.
