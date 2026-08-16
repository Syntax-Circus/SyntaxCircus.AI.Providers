# Getting Started with SyntaxCircus.AI.Providers

This guide walks you through installing and using SyntaxCircus.AI.Providers to make your first AI completion requests.

## Table of Contents
- [Installation](#installation)
- [Configuration](#configuration)
- [Dependency Injection Setup](#dependency-injection-setup)
- [Your First Request](#your-first-request)
- [Working with Structured Output](#working-with-structured-output)
- [Next Steps](#next-steps)

## Installation

Install the NuGet package:

```bash
dotnet add package SyntaxCircus.AI.Providers
```

Or via Package Manager Console:

```powershell
Install-Package SyntaxCircus.AI.Providers
```

## Configuration

Add API keys and model configuration to your `appsettings.json`:

```json
{
  "Anthropic": {
    "ApiKey": "sk-ant-...",
    "Model": "claude-opus-5",
    "MaxTokens": 4096
  },
  "Gemini": {
    "ApiKey": "your-gemini-api-key",
    "Model": "gemini-2.5-flash",
    "MaxOutputTokens": 4096
  }
}
```

**Security Note**: Never commit API keys to version control. Use:
- Environment variables (development)
- User secrets (`dotnet user-secrets`)
- Azure Key Vault or similar (production)

## Dependency Injection Setup

Add the AI providers to your service collection in `Program.cs`:

```csharp
using SyntaxCircus.AI.Providers;

var builder = WebApplicationBuilder.CreateBuilder(args);

// Register AI providers
builder.Services.AddAiProviders(builder.Configuration);

var app = builder.Build();
```

This registers both `AnthropicClient` and `GeminiClient` as injectable services.

## Your First Request

### Basic Text Completion (Anthropic)

Inject `AnthropicClient` and make a simple request:

```csharp
using SyntaxCircus.AI.Providers;

public class MyService
{
    private readonly AnthropicClient _anthropicClient;

    public MyService(AnthropicClient anthropicClient)
    {
        _anthropicClient = anthropicClient;
    }

    public async Task GetSummary(string text)
    {
        var result = await _anthropicClient.SendAsync(
            prompt: $"Summarize this in one sentence:\n\n{text}",
            systemPrompt: "You are a concise AI assistant.",
            ct: CancellationToken.None);

        if (!result.Success)
        {
            Console.WriteLine($"Error: {result.Error}");
            return;
        }

        Console.WriteLine($"Summary: {result.Content}");
        Console.WriteLine($"Tokens used: {result.TokensUsed}");
    }
}
```

### Basic Text Completion (Gemini)

```csharp
using SyntaxCircus.AI.Providers;

public class MyService
{
    private readonly GeminiClient _geminiClient;

    public MyService(GeminiClient geminiClient)
    {
        _geminiClient = geminiClient;
    }

    public async Task GetSummary(string text)
    {
        var result = await _geminiClient.SendAsync(
            prompt: $"Summarize this in one sentence:\n\n{text}",
            systemPrompt: "You are a concise AI assistant.");

        if (!result.Success)
        {
            Console.WriteLine($"Error: {result.Error}");
            return;
        }

        Console.WriteLine($"Summary: {result.Content}");
    }
}
```

## Understanding AiCompletionResult

All `SendAsync()` calls return an `AiCompletionResult`:

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

**Properties**:
- `Content`: The model's response (empty if failed)
- `TokensUsed`: Output tokens consumed (provider-dependent)
- `Error`: Error message (null if successful)
- `IsRateLimited`: True if rate limit hit (429)
- `RetryAfter`: When to retry after rate limit
- `Success`: Convenience property; true if Error is null

**Error Handling Pattern**:

```csharp
var result = await anthropicClient.SendAsync(prompt);

if (!result.Success)
{
    if (result.IsRateLimited)
    {
        // Back off until result.RetryAfter
        await Task.Delay(result.RetryAfter.Value - DateTimeOffset.UtcNow);
        // Retry the request
    }
    else
    {
        // Handle other errors: auth, timeout, server error, etc.
        logger.LogError($"Request failed: {result.Error}");
    }
    return;
}

Console.WriteLine(result.Content);
```

## Multi-Turn Conversations

Pass conversation history to continue a multi-turn conversation:

```csharp
var history = new List<AiChatMessage>
{
    new("user", "What is 2+2?"),
    new("assistant", "2+2 equals 4."),
    new("user", "What about 3+3?")
};

var result = await anthropicClient.SendAsync(
    prompt: "What about 3+3?",
    systemPrompt: "You are a math tutor.",
    conversationHistory: history);

Console.WriteLine(result.Content); // Output: 3+3 equals 6.
```

The history maintains context across multiple turns without you managing the full conversation state.

## Working with Structured Output

Both clients support schema-constrained responses for structured classification and extraction. Here's a quick example:

```csharp
var schema = """
{
  "type": "object",
  "properties": {
    "sentiment": { "type": "string", "enum": ["positive", "negative", "neutral"] },
    "confidence": { "type": "number", "minimum": 0, "maximum": 1 }
  },
  "required": ["sentiment", "confidence"]
}
""";

var result = await anthropicClient.SendAsync(
    prompt: "Analyze this review: 'Amazing product, highly recommend!'",
    responseJsonSchema: schema);

if (result.Success)
{
    // result.Content is guaranteed to be valid JSON matching the schema
    using var json = JsonDocument.Parse(result.Content);
    var sentiment = json.RootElement.GetProperty("sentiment").GetString();
    var confidence = json.RootElement.GetProperty("confidence").GetDouble();
    
    Console.WriteLine($"Sentiment: {sentiment} (confidence: {confidence})");
}
```

For a comprehensive guide to structured output, see [STRUCTURED_OUTPUT.md](STRUCTURED_OUTPUT.md).

## Next Steps

- **Explore Examples**: See [EXAMPLES.md](EXAMPLES.md) for more usage patterns
- **API Reference**: Read [API_REFERENCE.md](API_REFERENCE.md) for detailed method documentation
- **Integrate into Your Project**: Check [INTEGRATION_GUIDE.md](INTEGRATION_GUIDE.md) for common patterns
- **Handle Errors**: Visit [TROUBLESHOOTING.md](TROUBLESHOOTING.md) for common issues
- **Learn Architecture**: Review [ARCHITECTURE.md](ARCHITECTURE.md) to understand design decisions

## Common Questions

**Q: Can I use both Anthropic and Gemini clients in the same app?**  
Yes! Both are registered in dependency injection. Use whichever fits your needs per request.

**Q: How do I handle rate limits?**  
Check `result.IsRateLimited` and `result.RetryAfter`. Back off until the retry time, then retry. See [PERFORMANCE.md](PERFORMANCE.md) for retry strategies.

**Q: Does this library support streaming?**  
Not currently. This package focuses on simple request/response interactions.

**Q: How do I test code that uses these clients?**  
Inject a mock `HttpClient` or use the test utilities shown in [INTEGRATION_GUIDE.md](INTEGRATION_GUIDE.md).

**Q: What's the difference between Anthropic and Gemini implementations?**  
Both expose the same core API (`SendAsync`), but:
- Anthropic uses `x-api-key` header; Gemini uses `x-goog-api-key` header
- Configuration keys differ (`"Anthropic"` vs `"Gemini"`)
- Internal DTOs differ, but behavior is consistent
- Both support schema-constrained output

See [ARCHITECTURE.md](ARCHITECTURE.md) for design philosophy.
