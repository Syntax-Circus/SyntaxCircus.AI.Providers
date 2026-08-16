# SyntaxCircus.AI.Providers

[![Build](https://github.com/Syntax-Circus/SyntaxCircus.AI.Providers/actions/workflows/build.yml/badge.svg)](https://github.com/Syntax-Circus/SyntaxCircus.AI.Providers/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE.txt)

Low-level typed HTTP clients for the Anthropic Messages API and the Gemini `generateContent` API: request/response DTOs, rate-limit handling, and `Retry-After` parsing. **Not** a unified provider abstraction — a broad `IAiProvider`-style interface covering many vendors and modes (API, CLI, etc.) and a narrow structured-classification interface solve genuinely different problems, and forcing them into one shared abstraction serves neither well. This package is just the HTTP plumbing both kinds of consumer otherwise reimplement identically.

> **No support guaranteed.** Published as-is and maintained on a best-effort basis. Issues and PRs are welcome, but there's no SLA — fork it or vendor what you need if that's not enough.

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
        // back off until result.RetryAfter
    }
    // result.Error
}
```

`GeminiClient.SendAsync` has the same shape, plus an optional `responseJsonSchema` parameter — pass a raw JSON Schema string to constrain Gemini's output to `application/json` matching that schema (structured classification, extraction, etc.).

## A note on the API key

`GeminiClient` sends the key via the `x-goog-api-key` header, not the `?key=` query-string parameter some sample code uses. A key in the URL ends up in server logs, proxy logs, and the `Referer` header of any request the response triggers — a real leak vector for something meant to stay secret. `AnthropicClient` uses `x-api-key`, which was never at risk of this since Anthropic's API never supported a query-string key.

## Contributing

Issues and pull requests are welcome:
- Keep changes focused, with a clear description of the behavior change.
- Match the existing code style (see `.editorconfig`).
- Call out any breaking changes to the public API in your PR description.

## License

MIT — see [LICENSE.txt](LICENSE.txt).
