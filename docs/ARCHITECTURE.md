# Architecture & Design

Deep dive into the architecture, design decisions, and component relationships in SyntaxCircus.AI.Providers.

## Table of Contents
- [Package Purpose](#package-purpose)
- [Component Overview](#component-overview)
- [Data Flow](#data-flow)
- [Error Handling Philosophy](#error-handling-philosophy)
- [Key Design Decisions](#key-design-decisions)
- [Provider Abstraction](#provider-abstraction)
- [Rate Limiting & Retry Logic](#rate-limiting--retry-logic)

---

## Package Purpose

**SyntaxCircus.AI.Providers** provides low-level, typed HTTP clients for AI provider APIs — **not** a unified abstraction.

### Why Not a Unified Abstraction?

A single `IAiProvider` interface that covers all vendors and modes (API, CLI, streaming, batch, etc.) serves no use case well:
- **Breadth Problem**: Generic interface masks provider-specific capabilities and limitations
- **Coverage Problem**: Feature parity is impossible (streaming vs. request/response, token limits, model-specific parameters)
- **Impedance Mismatch**: Lowest-common-denominator interface loses useful provider nuances

### What We Do Instead

This package provides **provider-specific HTTP plumbing** that two kinds of consumers can use:
1. **Broad abstractions** (managed agent platforms, unified chat UIs) build their own adapter layer on top
2. **Narrow abstractions** (structured-classification, structured-extraction) use the client directly

Result: Both consumers get the specifics they need, and neither is forced into an ill-fitting mold.

---

## Component Overview

```
┌─────────────────────────────────────────────────────────────┐
│  Application Layer (Your Code)                              │
├─────────────────────────────────────────────────────────────┤
│  Dependency Injection (DI)                                  │
│  - AnthropicClient (singleton HttpClient)                   │
│  - GeminiClient (singleton HttpClient)                      │
├─────────────────────────────────────────────────────────────┤
│  Provider Clients                                           │
│  ┌──────────────────────┐  ┌──────────────────────┐         │
│  │  AnthropicClient     │  │  GeminiClient        │         │
│  │  - SendAsync()       │  │  - SendAsync()       │         │
│  └──────────────────────┘  └──────────────────────┘         │
├─────────────────────────────────────────────────────────────┤
│  Request Building                                           │
│  - Parameter validation                                     │
│  - Schema validation (Anthropic)                            │
│  - Request DTO serialization                                │
├─────────────────────────────────────────────────────────────┤
│  HTTP Transport (HttpClient)                                │
│  - x-api-key / x-goog-api-key headers                       │
│  - Request/response serialization (JSON)                    │
├─────────────────────────────────────────────────────────────┤
│  Provider API Endpoints                                     │
│  - Anthropic: POST /v1/messages                             │
│  - Gemini: POST /v1beta/models/:model:generateContent       │
├─────────────────────────────────────────────────────────────┤
│  Response Handling                                          │
│  - Status code parsing                                      │
│  - Retry-After header extraction                            │
│  - Response deserialization & validation                    │
│  - Error message mapping                                    │
├─────────────────────────────────────────────────────────────┤
│  Result Envelope (AiCompletionResult)                       │
│  - Content, TokensUsed, Error, IsRateLimited, RetryAfter    │
└─────────────────────────────────────────────────────────────┘
```

### Key Components

| Component | Responsibility |
|-----------|-----------------|
| `AnthropicClient` | Send requests to Anthropic API, handle responses, map errors |
| `GeminiClient` | Send requests to Gemini API, handle responses, map errors |
| `AiCompletionResult` | Stable envelope for all outcomes (success, rate limit, error) |
| `AiChatMessage` | Conversation message (role + content) |
| `SchemaValidator` | Validate JSON schemas before sending to Anthropic |
| `*ClientOptions` | Configuration (API key, model, token limits) |
| `AiProvidersServiceCollectionExtensions` | DI registration helper |

---

## Data Flow

### Request Flow

```
User Code
  │
  ├─→ anthropicClient.SendAsync(prompt, schema?, ...)
  │
  ├─→ Validate parameters
  │    - Check API key configured
  │    - If schema provided, validate schema structure (Anthropic only)
  │
  ├─→ Build request DTO
  │    - Append prompt to conversation history (if provided)
  │    - Serialize to JSON
  │    - Add provider-specific headers (x-api-key, version, etc.)
  │
  ├─→ Send via HttpClient
  │    - POST to provider endpoint
  │    - Await response
  │
  ├─→ Parse response
  │    - Check status code
  │    - Extract headers (Retry-After if 429)
  │    - Deserialize response body
  │
  ├─→ Validate response
  │    - If schema provided, validate response matches schema
  │    - If JSON parsing fails, mark as error
  │
  └─→ Return AiCompletionResult
       - Content: Model response text
       - TokensUsed: Output tokens (if provided)
       - Error: Error message (if failed)
       - IsRateLimited: True if 429 hit
       - RetryAfter: When to retry if rate limited
```

### Error Handling Flow

```
API Response
  │
  ├─ 401 (Unauthorized)  → "Invalid API key"
  ├─ 429 (Too Many)      → "Rate limit exceeded" (IsRateLimited=true)
  ├─ 5xx (Server Error)  → "API error (500)"
  ├─ Timeout             → "Request timed out"
  ├─ HTTP Error          → "HTTP error: ..."
  ├─ Malformed JSON      → "Malformed response from API"
  ├─ Empty Response      → "Empty response from API"
  ├─ Invalid Schema      → "Invalid schema: ..."
  ├─ Schema Mismatch     → "Response does not conform to schema"
  │
  └─→ All return AiCompletionResult with Error set (Success=false)
       No exceptions thrown (non-exception-based error handling)
```

---

## Error Handling Philosophy

**Non-Exception-Based**: All expected errors return `AiCompletionResult` errors, not exceptions.

### Why?

1. **Rate Limits are Not Exceptional**: Rate limiting is normal operation, not an error state. Throwing exceptions for rate limits forces exception-driven control flow for normal scenarios.
2. **Explicit Error Handling**: Callers must consciously handle errors (`if (!result.Success)`). Easier to spot error handling paths in code review.
3. **Composability**: Functional error handling (returning results) is more composable than exception-driven flow for batching, retries, and fallbacks.
4. **Performance**: No exception overhead for expected failure modes.

### What Exceptions Can Occur?

Only programming errors throw exceptions:
- `ArgumentNullException` when required parameters are null
- Invalid operations due to API configuration issues

Normal failures (auth, rate limits, server errors, timeouts) all return as `AiCompletionResult`.

### Usage Pattern

```csharp
// Explicit error handling - no try/catch needed
var result = await client.SendAsync(prompt);

if (!result.Success)
{
    if (result.IsRateLimited)
    {
        // Handle rate limit: back off and retry
    }
    else
    {
        // Handle other errors: auth, timeout, server error
    }
}

// Optional: Convert to exception-based if needed by downstream code
if (!result.Success)
    throw new InvalidOperationException(result.Error);
```

---

## Key Design Decisions

### 1. Provider-Specific Clients (Not Unified Interface)

**Decision**: Separate `AnthropicClient` and `GeminiClient`, not a single `IAiProvider`.

**Rationale**:
- Anthropic and Gemini have fundamentally different APIs (message structure, auth headers, response formats)
- A unified interface would hide these differences, creating a leaky abstraction
- Consumers building on top can choose which client best fits their needs

### 2. Optional Schema Support (Backward Compatible)

**Decision**: Add `responseJsonSchema` parameter (optional).

**Rationale**:
- Preserves all existing code without modification
- New functionality opt-in, not breaking change
- Follows principle of minimal surface area change

### 3. Anthropic Structured Output

**Decision**: Use Anthropic tool use to force structured JSON output when `responseJsonSchema` is provided.

**Rationale**:
- Fail fast: the request explicitly asks for structured output
- Keep the response shape consistent with Gemini's structured-output flow
- No extra public types or prompt-shape workarounds are needed

### 4. Non-Exception-Based Error Handling

**Decision**: Return errors in `AiCompletionResult`, not exceptions.

**Rationale**:
- Rate limits are expected, not exceptional
- Composable error handling (easier to retry, batch, fallback)
- Explicit error paths in code (easier to review)
- No exception overhead for normal failures

### 5. Response Validation for Schemas

**Decision**: Validate response conforms to schema after receiving it.

**Rationale**:
- Defensive: catch API anomalies (model returning wrong type)
- Type-safe: guarantee response matches schema before parsing
- Early error detection: fail fast rather than runtime parse errors

### 6. No Streaming Support

**Decision**: Request/response only, no streaming.

**Rationale**:
- Simpler API surface
- Easier to test and reason about
- Streaming requires different architecture (channels, backpressure)
- Use case separation: streaming consumers can build their own client

### 7. HttpClient Reuse (Not Per-Request)

**Decision**: Single `HttpClient` instance per provider, injected via DI.

**Rationale**:
- Connection pooling: reuse TCP connections across requests
- DNS caching: avoid repeated lookups
- Better performance: fewer resource allocations
- Standard .NET pattern

---

## Provider Abstraction

### Anthropic Implementation

- **API Endpoint**: `POST https://api.anthropic.com/v1/messages`
- **Auth**: `x-api-key` header
- **Request Shape**: Messages array, system prompt, max tokens
- **Response**: Content array (usually 1 text block)
- **Schema Support**: `output_config.format.type: "json_schema"`
- **Schema Validation**: Client-side (optional)

### Gemini Implementation

- **API Endpoint**: `POST https://generativelanguage.googleapis.com/v1beta/models/:model:generateContent`
- **Auth**: `x-goog-api-key` header
- **Request Shape**: Contents array (flattened), system instruction, generation config
- **Response**: Candidates array with content
- **Schema Support**: `generationConfig.responseSchema`
- **Schema Validation**: Provider-side (no client-side validation)

### Similarity

Both clients expose identical public API:
- `SendAsync(prompt, systemPrompt?, conversationHistory?, responseJsonSchema?, ...)`
- Return `AiCompletionResult` with consistent error handling
- Support schemas with identical format (JSON Schema)

---

## Rate Limiting & Retry Logic

### Rate Limit Handling

When provider returns 429 (Too Many Requests):

```csharp
var result = await client.SendAsync(prompt);

if (result.IsRateLimited)
{
    var waitTime = result.RetryAfter.Value - DateTimeOffset.UtcNow;
    
    // Wait until rate limit window closes
    await Task.Delay(waitTime);
    
    // Retry the request
    result = await client.SendAsync(prompt);
}
```

### Retry Strategy

Recommended retry pattern with exponential backoff:

```csharp
async Task<string> SendWithRetry(string prompt, int maxRetries = 3)
{
    var attempt = 0;
    var baseDelay = TimeSpan.FromSeconds(1);

    while (attempt < maxRetries)
    {
        var result = await client.SendAsync(prompt);
        
        if (result.Success) return result.Content;
        
        if (!result.IsRateLimited)
            throw new Exception($"Failed: {result.Error}");
        
        // Wait: base delay * 2^attempt (1s, 2s, 4s)
        var delay = baseDelay.Multiply(Math.Pow(2, attempt));
        await Task.Delay(delay);
        
        attempt++;
    }
    
    throw new Exception("Max retries exceeded");
}
```

### Provider-Specific Behavior

- **Anthropic**: Provides `Retry-After` header with backoff window
- **Gemini**: Provides similar retry header behavior
- **Both**: Package parses and exposes via `result.RetryAfter`

---

## Security Considerations

### API Key Handling

- **Header Transport**: Keys sent via `x-api-key` / `x-goog-api-key` headers, not query string
- **No Logging**: Keys are not logged or stored
- **Configuration**: Load from secure sources (user secrets, environment variables, Key Vault)

### Data Privacy

- **No Caching**: Requests/responses not cached by package
- **No Retention**: No data retained after request completes
- **TLS**: All communication via HTTPS
- **Delegation**: Data privacy policy depends on provider (Anthropic, Google)

---

See [DESIGN_PATTERNS.md](DESIGN_PATTERNS.md) for common patterns, or [PERFORMANCE.md](PERFORMANCE.md) for optimization strategies.
