# Design Patterns & Best Practices

Common patterns for using SyntaxCircus.AI.Providers effectively.

## Table of Contents
- [Provider Selection Pattern](#provider-selection-pattern)
- [Conversation Management](#conversation-management)
- [Error Handling Strategies](#error-handling-strategies)
- [Schema Design Patterns](#schema-design-patterns)
- [Dependency Injection](#dependency-injection)
- [Testing Patterns](#testing-patterns)

---

## Provider Selection Pattern

### Single Provider (Recommended for Most Cases)

Choose one provider and stick with it:

```csharp
public class SummarizeService
{
    private readonly AnthropicClient _client;

    public SummarizeService(AnthropicClient client)
    {
        _client = client;
    }

    public async Task<string> Summarize(string text)
    {
        var result = await _client.SendAsync(
            prompt: $"Summarize: {text}",
            systemPrompt: "Be concise.");
        
        return result.Success ? result.Content : throw new Exception(result.Error);
    }
}
```

**Pros**: Simple, direct, predictable  
**Cons**: No fallback if provider is down

### Fallback Pattern

Fallback to a second provider if the first is rate-limited or unavailable:

```csharp
public class ResilientAiService
{
    private readonly AnthropicClient _anthropic;
    private readonly GeminiClient _gemini;

    public ResilientAiService(AnthropicClient anthropic, GeminiClient gemini)
    {
        _anthropic = anthropic;
        _gemini = gemini;
    }

    public async Task<string> GetResponse(string prompt)
    {
        // Try Anthropic first
        var result = await _anthropic.SendAsync(prompt);
        if (result.Success)
            return result.Content;

        // Fallback to Gemini
        var fallback = await _gemini.SendAsync(prompt);
        if (fallback.Success)
            return fallback.Content;

        throw new Exception($"Both providers failed: {result.Error}, {fallback.Error}");
    }
}
```

**Pros**: Resilient to outages, better availability  
**Cons**: More complex, potential cost multiplier

### Cost-Optimized Pattern

Use cheaper provider (Gemini) for simple tasks, expensive (Anthropic) for complex:

```csharp
public class CostOptimizedService
{
    private readonly GeminiClient _gemini;
    private readonly AnthropicClient _anthropic;

    public async Task<string> Process(string text, string task)
    {
        return task switch
        {
            "summarize" or "translate" => 
                await SimpleTask(_gemini, text, task),
            "analyze" or "extract" => 
                await ComplexTask(_anthropic, text, task),
            _ => throw new ArgumentException("Unknown task")
        };
    }

    private async Task<string> SimpleTask(GeminiClient client, string text, string task)
    {
        var result = await client.SendAsync($"{task}: {text}");
        return result.Success ? result.Content : throw new Exception(result.Error);
    }

    private async Task<string> ComplexTask(AnthropicClient client, string text, string task)
    {
        var result = await client.SendAsync($"{task}: {text}");
        return result.Success ? result.Content : throw new Exception(result.Error);
    }
}
```

---

## Conversation Management

### Stateful Session

Manage conversation state in a service:

```csharp
public class ConversationSession
{
    private readonly AnthropicClient _client;
    private readonly List<AiChatMessage> _history = new();
    private readonly string _systemPrompt;

    public ConversationSession(AnthropicClient client, string systemPrompt)
    {
        _client = client;
        _systemPrompt = systemPrompt;
    }

    public async Task<string> Send(string userMessage)
    {
        var result = await _client.SendAsync(
            prompt: userMessage,
            systemPrompt: _systemPrompt,
            conversationHistory: _history);

        if (!result.Success)
            throw new Exception($"Request failed: {result.Error}");

        // Store both user and assistant messages
        _history.Add(new AiChatMessage("user", userMessage));
        _history.Add(new AiChatMessage("assistant", result.Content));

        return result.Content;
    }

    public void Reset() => _history.Clear();

    public int TurnCount => _history.Count / 2;
}
```

### Stateless (Replay Pattern)

Reconstruct conversation from message log:

```csharp
public class StatelessConversation
{
    private readonly AnthropicClient _client;
    private readonly List<(string Role, string Content)> _messages = new();

    public async Task<string> AddTurn(string userMessage)
    {
        _messages.Add(("user", userMessage));

        // Convert message log to chat history
        var history = _messages
            .Where(m => m.Role != "user" || _messages.Last().Role != m.Role)
            .Select(m => new AiChatMessage(m.Role, m.Content))
            .ToList();

        var result = await _client.SendAsync(userMessage, conversationHistory: history);

        if (result.Success)
        {
            _messages.Add(("assistant", result.Content));
            return result.Content;
        }

        throw new Exception($"Request failed: {result.Error}");
    }
}
```

**Stateful**: Pros—simpler, Cons—memory usage with long conversations  
**Stateless**: Pros—scalable, Cons—replay pattern complexity

---

## Error Handling Strategies

### Fail-Fast

Throw immediately on any error:

```csharp
public async Task<string> Summarize(string text)
{
    var result = await client.SendAsync(prompt: $"Summarize: {text}");
    
    if (!result.Success)
        throw new InvalidOperationException($"Failed to summarize: {result.Error}");
    
    return result.Content;
}
```

### Retry-on-Failure

Retry with exponential backoff:

```csharp
public async Task<string> SendWithRetry(string prompt, int maxAttempts = 3)
{
    for (int i = 0; i < maxAttempts; i++)
    {
        var result = await client.SendAsync(prompt);
        
        if (result.Success)
            return result.Content;
        
        if (!result.IsRateLimited)
            throw new Exception(result.Error);
        
        // Wait before retrying
        var delay = TimeSpan.FromSeconds(Math.Pow(2, i));
        await Task.Delay(delay);
    }
    
    throw new Exception("Max attempts exceeded");
}
```

### Graceful Degradation

Return cached/default value on error:

```csharp
public async Task<string> GetSummary(string text, string? cachedSummary = null)
{
    var result = await client.SendAsync($"Summarize: {text}");
    
    if (result.Success)
        return result.Content;
    
    // Fall back to cached or generic value
    return cachedSummary ?? "Summary unavailable";
}
```

### Circuit Breaker

Stop retrying after repeated failures:

```csharp
public class CircuitBreaker
{
    private int _failureCount = 0;
    private readonly int _failureThreshold = 5;
    private DateTimeOffset _lastFailureTime = DateTimeOffset.MinValue;
    private readonly TimeSpan _resetTimeout = TimeSpan.FromMinutes(1);

    public async Task<string> Send(Func<Task<AiCompletionResult>> operation)
    {
        if (_failureCount >= _failureThreshold)
        {
            var timeSinceLastFailure = DateTimeOffset.UtcNow - _lastFailureTime;
            if (timeSinceLastFailure < _resetTimeout)
                throw new InvalidOperationException("Circuit breaker is open");
            
            _failureCount = 0;  // Reset
        }

        var result = await operation();
        
        if (result.Success)
        {
            _failureCount = 0;
            return result.Content;
        }

        _failureCount++;
        _lastFailureTime = DateTimeOffset.UtcNow;
        throw new Exception(result.Error);
    }
}
```

---

## Schema Design Patterns

### Simple Enum Classification

For binary or categorical output:

```csharp
var schema = """
{
  "type": "object",
  "properties": {
    "classification": { 
      "type": "string", 
      "enum": ["spam", "important", "normal", "archived"] 
    }
  },
  "required": ["classification"]
}
""";
```

### Scored Classification

When you need confidence/reasoning:

```csharp
var schema = """
{
  "type": "object",
  "properties": {
    "classification": { "type": "string", "enum": ["positive", "negative"] },
    "confidence": { "type": "number", "minimum": 0, "maximum": 1 },
    "reasoning": { "type": "string" }
  },
  "required": ["classification", "confidence"]
}
""";
```

### Array Extraction

For multiple items:

```csharp
var schema = """
{
  "type": "object",
  "properties": {
    "items": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "name": { "type": "string" },
          "type": { "type": "string" }
        },
        "required": ["name"]
      },
      "minItems": 1
    }
  },
  "required": ["items"]
}
""";
```

---

## Dependency Injection

### Basic Setup

```csharp
// Program.cs
builder.Services.AddAiProviders(builder.Configuration);

// appsettings.json
{
  "Anthropic": { "ApiKey": "...", "Model": "claude-opus-5", "MaxTokens": 4096 },
  "Gemini": { "ApiKey": "...", "Model": "gemini-2.5-flash", "MaxOutputTokens": 4096 }
}
```

### Registering Services

```csharp
builder.Services.AddScoped<SummarizeService>();
builder.Services.AddScoped<ChatSession>();
builder.Services.AddScoped<ConversationSession>();
```

### Using in Controllers/Services

```csharp
[ApiController]
[Route("api/summarize")]
public class SummarizeController
{
    private readonly SummarizeService _service;

    public SummarizeController(SummarizeService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<IActionResult> Summarize(string text)
    {
        var summary = await _service.Summarize(text);
        return Ok(summary);
    }
}
```

---

## Testing Patterns

### Unit Test with Mock HttpClient

```csharp
[TestClass]
public class SummarizeServiceTests
{
    [TestMethod]
    public async Task Summarize_ReturnsExpectedContent()
    {
        // Arrange
        var response = """{"content":[{"text":"Test summary"}],"usage":{"output_tokens":10}}""";
        var handler = new StubHttpMessageHandler(_ => 
            new HttpResponseMessage(HttpStatusCode.OK) 
            { 
                Content = new StringContent(response, Encoding.UTF8, "application/json")
            });
        
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new AnthropicClientOptions 
        { 
            ApiKey = "test-key",
            Model = "test-model",
            MaxTokens = 1024
        });
        var client = new AnthropicClient(httpClient, options);
        var service = new SummarizeService(client);

        // Act
        var result = await service.Summarize("Test text");

        // Assert
        Assert.AreEqual("Test summary", result);
    }
}
```

### Integration Test with Real API

```csharp
[TestClass]
public class RealProviderTests
{
    [TestMethod]
    [Ignore("Requires real API key")]
    public async Task SendAsync_WithRealApi_Succeeds()
    {
        var httpClient = new HttpClient();
        var options = Options.Create(new AnthropicClientOptions
        {
            ApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")!,
            Model = "claude-opus-5",
            MaxTokens = 1024
        });
        var client = new AnthropicClient(httpClient, options);

        var result = await client.SendAsync("Say 'hello'");

        Assert.IsTrue(result.Success);
        Assert.IsTrue(result.Content.Contains("hello", StringComparison.OrdinalIgnoreCase));
    }
}
```

---

See [ARCHITECTURE.md](ARCHITECTURE.md) for design philosophy, or [EXAMPLES.md](EXAMPLES.md) for more code samples.
