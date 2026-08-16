# Usage Examples

Practical examples for common use cases with SyntaxCircus.AI.Providers.

## Table of Contents
- [Basic Text Completion](#basic-text-completion)
- [Multi-Turn Conversations](#multi-turn-conversations)
- [Structured Output (Schemas)](#structured-output-schemas)
- [Error Handling](#error-handling)
- [Batch Processing](#batch-processing)
- [Integration Patterns](#integration-patterns)

---

## Basic Text Completion

### Simple Question and Answer

```csharp
public class QaService
{
    private readonly AnthropicClient _client;

    public QaService(AnthropicClient client)
    {
        _client = client;
    }

    public async Task<string> AskQuestion(string question)
    {
        var result = await _client.SendAsync(
            prompt: question,
            systemPrompt: "You are a helpful assistant.");

        if (!result.Success)
        {
            throw new InvalidOperationException($"Request failed: {result.Error}");
        }

        return result.Content;
    }
}
```

### Summarization

```csharp
public class SummaryService
{
    private readonly GeminiClient _client;

    public SummaryService(GeminiClient client)
    {
        _client = client;
    }

    public async Task<string> Summarize(string text, int maxLength = 100)
    {
        var result = await _client.SendAsync(
            prompt: $"Summarize the following text in {maxLength} words or less:\n\n{text}",
            systemPrompt: "You are a concise summarization expert.");

        if (result.Success)
        {
            return result.Content;
        }

        throw new InvalidOperationException($"Summarization failed: {result.Error}");
    }
}
```

### Translation

```csharp
public class TranslationService
{
    private readonly AnthropicClient _client;

    public TranslationService(AnthropicClient client)
    {
        _client = client;
    }

    public async Task<string> Translate(string text, string targetLanguage)
    {
        var result = await _client.SendAsync(
            prompt: $"Translate to {targetLanguage}:\n\n{text}",
            systemPrompt: $"You are a professional translator. Always respond with ONLY the translated text, nothing else.");

        if (!result.Success)
        {
            throw new InvalidOperationException($"Translation failed: {result.Error}");
        }

        return result.Content;
    }
}
```

---

## Multi-Turn Conversations

### Chat Session

```csharp
public class ChatSession
{
    private readonly AnthropicClient _client;
    private readonly List<AiChatMessage> _history = new();
    private const string SystemPrompt = "You are a helpful assistant. Keep responses concise.";

    public ChatSession(AnthropicClient client)
    {
        _client = client;
    }

    public async Task<string> SendMessage(string userMessage)
    {
        // Send message with history
        var result = await _client.SendAsync(
            prompt: userMessage,
            systemPrompt: SystemPrompt,
            conversationHistory: _history);

        if (!result.Success)
        {
            throw new InvalidOperationException($"Chat failed: {result.Error}");
        }

        // Store in history for next turn
        _history.Add(new AiChatMessage("user", userMessage));
        _history.Add(new AiChatMessage("assistant", result.Content));

        return result.Content;
    }

    public void Reset()
    {
        _history.Clear();
    }
}

// Usage
var session = new ChatSession(anthropicClient);

string response1 = await session.SendMessage("What is 2+2?");
// Response: 2+2 equals 4.

string response2 = await session.SendMessage("What about 3+3?");
// Response: 3+3 equals 6. (Has context from previous turn)
```

### Interview Simulation

```csharp
public class InterviewSimulator
{
    private readonly GeminiClient _client;
    private readonly List<AiChatMessage> _history = new();
    private readonly string _role;
    private readonly string _company;

    public InterviewSimulator(GeminiClient client, string role, string company)
    {
        _client = client;
        _role = role;
        _company = company;
    }

    public async Task<string> GetNextQuestion()
    {
        var prompt = _history.Count == 0
            ? "Start an interview for a software engineer position at Microsoft. Ask the first question."
            : "Ask the next interview question based on the conversation so far.";

        var result = await _client.SendAsync(
            prompt: prompt,
            systemPrompt: $"You are an interviewer at {_company} conducting a technical interview for a {_role} position.",
            conversationHistory: _history);

        if (!result.Success)
        {
            throw new InvalidOperationException($"Interview failed: {result.Error}");
        }

        _history.Add(new AiChatMessage("assistant", result.Content));
        return result.Content;
    }

    public async Task<string> ProvideAnswer(string answer)
    {
        _history.Add(new AiChatMessage("user", answer));

        var result = await _client.SendAsync(
            prompt: "Provide feedback on the answer and ask a follow-up question.",
            systemPrompt: $"You are an interviewer at {_company}. Provide constructive feedback.",
            conversationHistory: _history);

        if (!result.Success)
        {
            throw new InvalidOperationException($"Feedback failed: {result.Error}");
        }

        _history.Add(new AiChatMessage("assistant", result.Content));
        return result.Content;
    }
}
```

---

## Structured Output (Schemas)

### Sentiment Analysis

```csharp
public class SentimentAnalyzer
{
    private readonly AnthropicClient _client;

    private static readonly string SentimentSchema = """
    {
      "type": "object",
      "properties": {
        "sentiment": { "type": "string", "enum": ["positive", "negative", "neutral"] },
        "confidence": { "type": "number", "minimum": 0, "maximum": 1 },
        "reasoning": { "type": "string" }
      },
      "required": ["sentiment", "confidence", "reasoning"]
    }
    """;

    public SentimentAnalyzer(AnthropicClient client)
    {
        _client = client;
    }

    public async Task<(string Sentiment, double Confidence, string Reasoning)> Analyze(string text)
    {
        var result = await _client.SendAsync(
            prompt: $"Analyze the sentiment of: {text}",
            responseJsonSchema: SentimentSchema);

        if (!result.Success)
        {
            throw new InvalidOperationException($"Analysis failed: {result.Error}");
        }

        using var json = JsonDocument.Parse(result.Content);
        var root = json.RootElement;

        var sentiment = root.GetProperty("sentiment").GetString()!;
        var confidence = root.GetProperty("confidence").GetDouble();
        var reasoning = root.GetProperty("reasoning").GetString()!;

        return (sentiment, confidence, reasoning);
    }
}

// Usage
var analyzer = new SentimentAnalyzer(anthropicClient);
var (sentiment, confidence, reasoning) = await analyzer.Analyze("I love this product!");
Console.WriteLine($"Sentiment: {sentiment} ({confidence:P0})\n{reasoning}");
```

### Information Extraction

```csharp
public class EmailExtractor
{
    private readonly GeminiClient _client;

    private static readonly string ExtractionSchema = """
    {
      "type": "object",
      "properties": {
        "sender": { "type": "string" },
        "subject": { "type": "string" },
        "main_points": { "type": "array", "items": { "type": "string" } },
        "action_items": { "type": "array", "items": { "type": "string" } },
        "urgency": { "type": "string", "enum": ["low", "medium", "high"] }
      },
      "required": ["sender", "subject", "main_points", "action_items", "urgency"]
    }
    """;

    public EmailExtractor(GeminiClient client)
    {
        _client = client;
    }

    public async Task<EmailSummary> Extract(string emailContent)
    {
        var result = await _client.SendAsync(
            prompt: $"Extract information from this email:\n\n{emailContent}",
            responseJsonSchema: ExtractionSchema);

        if (!result.Success)
        {
            throw new InvalidOperationException($"Extraction failed: {result.Error}");
        }

        using var json = JsonDocument.Parse(result.Content);
        var root = json.RootElement;

        var mainPoints = root.GetProperty("main_points")
            .EnumerateArray()
            .Select(e => e.GetString()!)
            .ToList();

        var actionItems = root.GetProperty("action_items")
            .EnumerateArray()
            .Select(e => e.GetString()!)
            .ToList();

        return new EmailSummary(
            Sender: root.GetProperty("sender").GetString()!,
            Subject: root.GetProperty("subject").GetString()!,
            MainPoints: mainPoints,
            ActionItems: actionItems,
            Urgency: root.GetProperty("urgency").GetString()!
        );
    }
}

record EmailSummary(string Sender, string Subject, List<string> MainPoints, List<string> ActionItems, string Urgency);
```

---

## Error Handling

### Retry with Exponential Backoff

```csharp
public class ResilientAiService
{
    private readonly AnthropicClient _client;
    private readonly ILogger<ResilientAiService> _logger;

    public ResilientAiService(AnthropicClient client, ILogger<ResilientAiService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<string> SendWithRetry(string prompt, int maxRetries = 3)
    {
        var retryCount = 0;
        var baseDelay = TimeSpan.FromSeconds(1);

        while (retryCount < maxRetries)
        {
            var result = await _client.SendAsync(prompt);

            if (result.Success)
            {
                return result.Content;
            }

            if (!result.IsRateLimited)
            {
                throw new InvalidOperationException($"Request failed: {result.Error}");
            }

            // Rate limited - wait and retry
            var waitTime = result.RetryAfter.Value - DateTimeOffset.UtcNow;
            _logger.LogWarning($"Rate limited. Waiting {waitTime.TotalSeconds} seconds...");
            await Task.Delay(waitTime);

            retryCount++;
        }

        throw new InvalidOperationException("Max retries exceeded");
    }
}
```

### Graceful Degradation

```csharp
public class FallbackAiService
{
    private readonly AnthropicClient _anthropic;
    private readonly GeminiClient _gemini;

    public FallbackAiService(AnthropicClient anthropic, GeminiClient gemini)
    {
        _anthropic = anthropic;
        _gemini = gemini;
    }

    public async Task<string> GetResponse(string prompt)
    {
        // Try Anthropic first
        var result = await _anthropic.SendAsync(prompt);
        if (result.Success)
        {
            return result.Content;
        }

        if (result.IsRateLimited)
        {
            // Try Gemini if Anthropic is rate limited
            var fallbackResult = await _gemini.SendAsync(prompt);
            if (fallbackResult.Success)
            {
                return fallbackResult.Content;
            }
        }

        throw new InvalidOperationException($"All providers failed: {result.Error}");
    }
}
```

---

## Batch Processing

### Process Multiple Items

```csharp
public class BatchProcessor
{
    private readonly AnthropicClient _client;

    public BatchProcessor(AnthropicClient client)
    {
        _client = client;
    }

    public async Task<List<string>> ProcessBatch(List<string> items, string task)
    {
        var results = new List<string>();
        var semaphore = new SemaphoreSlim(5); // Max 5 concurrent requests

        var tasks = items.Select(async item =>
        {
            await semaphore.WaitAsync();
            try
            {
                var result = await _client.SendAsync(
                    prompt: $"{task}: {item}");

                lock (results)
                {
                    results.Add(result.Success ? result.Content : $"Error: {result.Error}");
                }
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        return results;
    }
}

// Usage
var processor = new BatchProcessor(anthropicClient);
var texts = new List<string> { "text1", "text2", "text3" };
var summaries = await processor.ProcessBatch(texts, "Summarize");
```

---

## Integration Patterns

### Dependency Injection with Configuration

```csharp
// In Program.cs
builder.Services.AddAiProviders(builder.Configuration);
builder.Services.AddScoped<SentimentAnalyzer>();
builder.Services.AddScoped<ChatSession>();

// In your service
public class MyService
{
    private readonly SentimentAnalyzer _analyzer;

    public MyService(SentimentAnalyzer analyzer)
    {
        _analyzer = analyzer;
    }

    public async Task DoWork()
    {
        var (sentiment, _, _) = await _analyzer.Analyze("Text to analyze");
    }
}
```

### Factory Pattern for Multiple Instances

```csharp
public class AiClientFactory
{
    private readonly IServiceProvider _serviceProvider;

    public AiClientFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public AnthropicClient CreateAnthropicClient() => _serviceProvider.GetRequiredService<AnthropicClient>();
    public GeminiClient CreateGeminiClient() => _serviceProvider.GetRequiredService<GeminiClient>();
}
```

---

See [STRUCTURED_OUTPUT.md](STRUCTURED_OUTPUT.md) for more detailed schema examples, or [PERFORMANCE.md](PERFORMANCE.md) for optimization strategies.
