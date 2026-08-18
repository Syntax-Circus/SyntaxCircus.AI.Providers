# SyntaxCircus.AI.Providers

[![Build](https://github.com/Syntax-Circus/SyntaxCircus.AI.Providers/actions/workflows/build.yml/badge.svg)](https://github.com/Syntax-Circus/SyntaxCircus.AI.Providers/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/SyntaxCircus.AI.Providers.svg)](https://www.nuget.org/packages/SyntaxCircus.AI.Providers)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE.txt)

Low-level typed HTTP clients for the Anthropic Messages API and the Gemini `generateContent` API: request/response DTOs, rate-limit handling, and `Retry-After` parsing. **Not** a unified provider abstraction — a broad `IAiProvider`-style interface covering many vendors and modes (API, CLI, etc.) and a narrow structured-classification interface solve genuinely different problems, and forcing them into one shared abstraction serves neither well. This package is just the HTTP plumbing both kinds of consumer otherwise reimplement identically.

> **No support guaranteed.** Published as-is and maintained on a best-effort basis. Issues and PRs are welcome, but there's no SLA — fork it or vendor what you need if that's not enough.

## 📖 Documentation

**Start here:**
- **[Getting Started](docs/GETTING_STARTED.md)** — Installation, setup, first requests
- **[Documentation Index](docs/INDEX.md)** — Full navigation by role (developer, AI agent, maintainer)

**By topic:**
- **[API Reference](docs/API_REFERENCE.md)** — Complete method signatures and error codes
- **[Examples](docs/EXAMPLES.md)** — Real-world usage patterns with copy-paste code
- **[Structured Output](docs/STRUCTURED_OUTPUT.md)** — Schema-constrained responses (JSON Schema guide)
- **[Architecture](docs/ARCHITECTURE.md)** — System design, data flow, error handling philosophy
- **[Integration Guide](docs/INTEGRATION_GUIDE.md)** — Adding to projects, DI setup, testing
- **[Design Patterns](docs/DESIGN_PATTERNS.md)** — Common patterns (provider selection, error handling, conversation management)
- **[Performance](docs/PERFORMANCE.md)** — Rate limiting, cost optimization, retry strategies
- **[Troubleshooting](docs/TROUBLESHOOTING.md)** — Common issues and solutions
- **[Contributing](docs/CONTRIBUTING.md)** — Code style, adding providers, PR process

## Setup

```csharp
builder.Services.AddAiProviders(builder.Configuration); // binds "Anthropic" and "Gemini", registers both typed clients
```

```json
{
  "Anthropic": { "ApiKey": "sk-ant-...", "Model": "claude-sonnet-5", "MaxTokens": 4096 },
  "Gemini": { "ApiKey": "...", "Model": "gemini-2.5-flash", "MaxOutputTokens": 4096 }
}
```

See [Getting Started](docs/GETTING_STARTED.md) for detailed setup instructions.

## Usage

```csharp
AiCompletionResult result = await anthropicClient.SendAsync(
    prompt: "Summarize this in one sentence.",
    systemPrompt: "You are a terse assistant.",
    conversationHistory: previousTurns);

if (!result.Success)
{
    if (result.IsRateLimited)
    {
        // back off until result.RetryAfter (see Performance guide)
    }
    // result.Error contains error message
}

// result.Content contains the response text
```

Both `AnthropicClient` and `GeminiClient` accept the same `responseJsonSchema` pattern for structured output. See [API Reference](docs/API_REFERENCE.md) for complete method documentation, or [Examples](docs/EXAMPLES.md) for real-world usage patterns.

### Runtime API keys

The standard overload obtains its key from the configured `Anthropic:ApiKey` or `Gemini:ApiKey` option. Applications that securely retrieve a user-specific key at runtime (for example, from an OS credential store) can use the overload that takes `apiKeyOverride` instead. The override is used only for that request; it is never written to configuration or logged by this package.

```csharp
string? apiKey = await credentialStore.GetAsync("MyApp", "Gemini", cancellationToken);
if (string.IsNullOrWhiteSpace(apiKey))
{
    return;
}

AiCompletionResult result = await geminiClient.SendAsync(
    prompt: "Summarize this crawl report.",
    apiKeyOverride: apiKey,
    ct: cancellationToken);
```

Use the normal configured-key overload for service-owned credentials. Use the runtime-key overload for per-user credentials; do not copy those secrets into `appsettings.json` merely to call a client.

### Structured Output / Schema-Constrained Responses

Both clients support schema-constrained responses for structured classification and extraction:

```csharp
var schema = """
{
  "type": "object",
  "properties": {
    "sentiment": { "type": "string", "enum": ["positive", "negative", "neutral"] },
    "confidence": { "type": "number" }
  },
  "required": ["sentiment", "confidence"]
}
""";

AiCompletionResult result = await anthropicClient.SendAsync(
    prompt: "Analyze this review: 'Amazing product, highly recommend!'",
    responseJsonSchema: schema);

if (!result.Success)
{
    // result.Error contains provider error, timeout, or schema/tool-use issues
}

// result.Content is guaranteed to be valid JSON conforming to the schema
var response = JsonSerializer.Deserialize<SentimentAnalysis>(result.Content);
```

Anthropic uses tool use under the hood to force a structured JSON response. Gemini uses its native JSON schema response mode.

## A note on the API key

`GeminiClient` sends both configured and runtime override keys via the `x-goog-api-key` header, not the `?key=` query-string parameter some sample code uses. A key in the URL ends up in server logs, proxy logs, and the `Referer` header of any request the response triggers — a real leak vector for something meant to stay secret. `AnthropicClient` sends its configured or runtime override key through `x-api-key`, which was never at risk of this since Anthropic's API never supported a query-string key.

## Contributing

Issues and pull requests are welcome. See [Contributing Guide](docs/CONTRIBUTING.md) for detailed guidelines on:
- Code style and conventions
- Adding new providers
- Testing requirements
- Documentation standards
- Pull request process

Quick summary:
- Keep changes focused, with a clear description of the behavior change.
- Match the existing code style (see `.editorconfig`).
- Add tests for new features.
- Call out any breaking changes to the public API in your PR description.

## License

MIT — see [LICENSE.txt](LICENSE.txt).
