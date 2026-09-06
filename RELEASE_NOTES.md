# Release Notes: Automatic HTTP Resilience (Unreleased)

## Improvements

### Retry + circuit breaker on all typed HTTP clients

`AnthropicClient`, `GeminiClient`, and `OpenAiCompatibleClient` now get automatic retry
(exponential backoff with jitter) and circuit-breaking on their underlying `HttpClient`,
via `SyntaxCircus.Http.Resilience`'s `AddResilientHttpClient`. Transport failures, timeouts,
and HTTP 408/500/502/503/504 responses are retried (up to 3 attempts); a circuit breaker
opens after sustained failure to fast-fail further calls until the provider recovers.

- **No public API change**: `AddAiProviders(IConfiguration)` still returns the same typed
  clients with the same constructors; this is purely additive resilience underneath the
  existing `HttpClient` registrations.
- **HTTP 429 is intentionally excluded** from the automatic retry (`aiMode: true`): all three
  clients already surface rate limiting to the caller as a non-throwing result
  (`AiCompletionResult.IsRateLimited` / `RetryAfter`, parsed via `RetryAfterParser`), so an
  automatic retry underneath would fight that caller-visible backoff contract instead of
  complementing it.
- New dependency: `SyntaxCircus.Http.Resilience` (0.2.1).

---

# Release Notes: OpenAiCompatibleClient Model Listing (Unreleased)

## New Features

### `OpenAiCompatibleClient.ListModelsAsync`

Lists the models available from an OpenAI-compatible endpoint's catalog (`GET {baseUrl}/models`), so a consumer can offer a live model picker instead of (or alongside) freeform text entry.

#### What's New

- **New Method**: `ListModelsAsync(CancellationToken)` and `ListModelsAsync(string? apiKeyOverride, string? baseUrlOverride, CancellationToken)` on `OpenAiCompatibleClient`, following the same override pattern as `SendAsync`.
- **New Types**: `AiModelInfo` (`Id`, `DisplayName`) and `AiModelsResult` (`Models`, `Error`, `Success`) — same non-exception-based error envelope as `AiCompletionResult`.
- **No API key required**: unlike `SendAsync`, a missing API key only omits the `Authorization` header rather than short-circuiting the call, since many catalog endpoints (OpenRouter's included) don't require authentication to list models.
- A `404` response is reported distinctly (`"This provider does not support listing models."`) since not every OpenAI-compatible endpoint implements this.

#### Usage Example

```csharp
var models = await openAiCompatibleClient.ListModelsAsync(
    apiKeyOverride: tenantApiKey,
    baseUrlOverride: tenantBaseUrl);

if (models.Success)
{
    foreach (var model in models.Models)
    {
        Console.WriteLine(model.DisplayName ?? model.Id);
    }
}
```

---

# Release Notes: OpenAI-Compatible Provider Support (Unreleased)

## New Features

### New `OpenAiCompatibleClient` for OpenAI-Compatible `chat/completions` Endpoints

A new typed client, `OpenAiCompatibleClient`, targets any OpenAI-compatible `chat/completions` API — OpenAI itself, OpenRouter, or a self-hosted gateway — bringing this package's non-exception-based error handling and rate-limit support to that provider family too.

#### What's New

- **New Client**: `OpenAiCompatibleClient` follows the same `SendAsync` overload ladder as `AnthropicClient`/`GeminiClient` (caller-supplied API key, then model), plus one further overload adding a caller-supplied `baseUrlOverride` — this provider's endpoint is commonly per-tenant or per-request rather than a single fixed host.
- **New Options**: `OpenAiCompatibleClientOptions` (`ApiKey`, `Model`, `BaseUrl`, `MaxTokens`). `Model` and `BaseUrl` default to empty, unlike the other providers' options.
- **DI Registration**: `AddAiProviders` now also registers `OpenAiCompatibleClient`, bound to the `"OpenAiCompatible"` configuration section — registered without a fixed `HttpClient.BaseAddress`, since the base URL is resolved per call.
- **Auth**: Sends the API key via a standard `Authorization: Bearer` header.

#### Usage Example

```csharp
var result = await openAiCompatibleClient.SendAsync(
    prompt: "What is the capital of France?",
    apiKeyOverride: tenantApiKey,
    systemPrompt: null,
    conversationHistory: null,
    responseJsonSchema: null,
    modelOverride: tenantModel,
    baseUrlOverride: tenantBaseUrl);

if (result.Success)
{
    Console.WriteLine(result.Content);
}
```

## Backward Compatibility

✅ **Fully backward compatible** — this is a purely additive new client and options type; no existing public API changed.

## What Changed

- New public class `OpenAiCompatibleClient`
- New public class `OpenAiCompatibleClientOptions`
- `AiProvidersServiceCollectionExtensions.AddAiProviders` now also registers `OpenAiCompatibleClient` and `OpenAiCompatibleClientOptions`

## Testing

- Added `OpenAiCompatibleClientTests` covering success parsing, missing API key/base URL short-circuiting, `apiKeyOverride`/`modelOverride`/`baseUrlOverride`, base URL normalization, 400/401/403/429/500 status handling, malformed JSON, and system-prompt/conversation-history mapping
- Added `OpenAiCompatibleClientOptionsTests` for option defaults
- Extended `AiProvidersServiceCollectionExtensionsTests` to cover the new registration and option binding

## API Changes Summary

- **Breaking Changes**: None
- **New Public Types**: `OpenAiCompatibleClient`, `OpenAiCompatibleClientOptions`
- **Modified Methods**: `AiProvidersServiceCollectionExtensions.AddAiProviders` (registers the new client/options; existing registrations unchanged)
- **Deprecated Methods**: None

---

# Release Notes: Anthropic Schema Support

## New Features

### Anthropic Client Now Supports First-Class Structured Output

The `AnthropicClient` now supports schema-constrained output via the `responseJsonSchema` parameter, bringing it to feature parity with `GeminiClient` for structured classification and extraction tasks.

#### What's New

- **Schema Parameter**: Pass a JSON Schema string to `AnthropicClient.SendAsync()` to constrain Claude's output to match your schema.
- **Client-Side Validation**: Schemas are validated client-side by default, catching malformed schemas before they reach the API.
- **Validation Bypass**: Optional `skipSchemaValidation` parameter allows advanced users to bypass client-side validation if needed.
- **Error Handling**: All schema-related errors (invalid schema, response parse failures, type mismatches) return stable `AiCompletionResult` errors without exceptions.

#### Usage Example

```csharp
var schema = """
{
  "type": "object",
  "properties": {
    "name": { "type": "string" },
    "email": { "type": "string" },
    "sentiment": { "type": "string", "enum": ["positive", "negative", "neutral"] }
  },
  "required": ["name", "email", "sentiment"]
}
""";

var result = await anthropicClient.SendAsync(
    prompt: "Extract info from: John Smith (john@example.com) loves our service!",
    responseJsonSchema: schema);

if (result.Success)
{
    var data = JsonSerializer.Deserialize<dynamic>(result.Content);
    // data is guaranteed to match the schema
}
```

## Backward Compatibility

✅ **Fully backward compatible** — existing code without schema parameters continues to work unchanged. All new parameters are optional.

## What Changed

- `AnthropicClient.SendAsync()` now accepts optional parameters:
  - `responseJsonSchema: string?` — JSON Schema string for structured output
  - `skipSchemaValidation: bool` — defaults to `false` to enable client-side validation

- New public class `SchemaValidator` provides JSON schema validation utilities
- New public record `SchemaValidationResult` represents schema validation outcomes

## Testing

- Added 10+ unit tests for schema validation
- Added 15+ unit tests for Anthropic schema support
- All tests verify schema validation, request format, response validation, and error handling
- Tested error cases: invalid schema, malformed responses, type mismatches, timeout/rate-limit stability

## API Changes Summary

- **Breaking Changes**: None
- **New Public Types**: `SchemaValidator`, `SchemaValidationResult`
- **Modified Methods**: `AnthropicClient.SendAsync()` (new optional parameters)
- **Deprecated Methods**: None

