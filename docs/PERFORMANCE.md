# Performance Guide

Optimizing cost, speed, and reliability of SyntaxCircus.AI.Providers usage.

## Table of Contents
- [Rate Limiting](#rate-limiting)
- [Token Optimization](#token-optimization)
- [Retry Strategies](#retry-strategies)
- [Timeout Configuration](#timeout-configuration)
- [Connection Pooling](#connection-pooling)
- [Cost Optimization](#cost-optimization)
- [Monitoring](#monitoring)

---

## Rate Limiting

### Understanding Rate Limits

API providers enforce rate limits:
- **Anthropic**: ~50k tokens/min, ~25k requests/day (varies by plan)
- **Gemini**: ~60 requests/min free tier, varies by plan

Rate limit responses return `AiCompletionResult.IsRateLimited == true`.

### Detecting Rate Limits

```csharp
var result = await client.SendAsync("prompt");

if (result.IsRateLimited)
{
    Console.WriteLine("Rate limited. Retry after backoff.");
    // Handle with exponential backoff
}
```

### Exponential Backoff

Recommended retry strategy:

```csharp
public async Task<AiCompletionResult> SendWithBackoff(
    Func<Task<AiCompletionResult>> operation,
    int maxRetries = 5)
{
    var baseDelay = TimeSpan.FromSeconds(1);

    for (int attempt = 0; attempt < maxRetries; attempt++)
    {
        var result = await operation();

        if (result.Success)
            return result;

        if (!result.IsRateLimited)
            throw new Exception(result.Error);

        // Exponential backoff: 1s, 2s, 4s, 8s, 16s
        var delay = TimeSpan.FromSeconds(
            baseDelay.TotalSeconds * Math.Pow(2, attempt));
        
        await Task.Delay(delay);
    }

    throw new Exception("Max retries exceeded");
}

// Usage
var result = await SendWithBackoff(() => client.SendAsync("prompt"));
```

### Request Batching

Group related requests to reduce overhead:

```csharp
public async Task<List<string>> ProcessBatch(List<string> items)
{
    var results = new List<string>();
    var delayBetweenRequests = TimeSpan.FromMilliseconds(100);

    foreach (var item in items)
    {
        var result = await client.SendAsync($"Process: {item}");
        results.Add(result.Content);

        // Add delay between requests to avoid rate limiting
        await Task.Delay(delayBetweenRequests);
    }

    return results;
}
```

### Rate Limit Headers

Providers may return rate limit info in response headers (check provider docs):

```csharp
// For future enhancement: extract from response headers
// Anthropic returns: retry-after-ms, x-ratelimit-limit-requests, etc.
```

---

## Token Optimization

### Token Counting

Estimate tokens before sending (approximate):
- English: ~1 token per 4 characters
- Code: ~1 token per 2-3 characters
- Structured data: varies

```csharp
public static int EstimateTokens(string text)
{
    // Rough estimate: divide by 4 for English
    return (int)Math.Ceiling(text.Length / 4.0);
}

public async Task<AiCompletionResult> SendWithTokenCheck(string prompt, int maxTokens = 8000)
{
    var promptTokens = EstimateTokens(prompt);
    
    if (promptTokens > maxTokens)
    {
        return new AiCompletionResult($"Prompt too long: {promptTokens} tokens exceeds limit {maxTokens}");
    }

    return await client.SendAsync(prompt);
}
```

### Prompt Optimization

Reduce token usage:

```csharp
// ❌ Inefficient: includes full history every time
var history = new List<AiChatMessage>
{
    new("user", "What's 2+2?"),
    new("assistant", "2+2=4"),
    new("user", "And 3+3?"),
    new("assistant", "3+3=6"),
    new("user", "And 5+5?")  // Only this matters
};

// ✅ Efficient: keep only relevant context
var optimizedHistory = new List<AiChatMessage>
{
    new("user", "And 5+5?")  // Latest question only
};

// ✅ System prompt reuse: don't repeat in every request
string systemPrompt = "You are a math tutor. Be concise.";  // Once
var result = await client.SendAsync("What's 7+7?", systemPrompt);
```

### Compression Techniques

```csharp
public async Task<string> SummarizeFirst(string longText)
{
    // If text is too long, summarize first
    if (longText.Length > 50000)
    {
        var summary = await client.SendAsync(
            $"Summarize in 100 words: {longText.Substring(0, 50000)}...");
        
        return summary.Success ? summary.Content : longText;
    }

    return longText;
}
```

---

## Retry Strategies

### Circuit Breaker

Stop retrying after repeated failures:

```csharp
public class CircuitBreaker
{
    private int _failureCount = 0;
    private readonly int _failureThreshold = 5;
    private DateTimeOffset _lastFailure = DateTimeOffset.MinValue;
    private readonly TimeSpan _resetTimeout = TimeSpan.FromMinutes(1);
    private CircuitState _state = CircuitState.Closed;

    public enum CircuitState { Closed, Open, HalfOpen }

    public async Task<AiCompletionResult> Execute(
        Func<Task<AiCompletionResult>> operation)
    {
        if (_state == CircuitState.Open)
        {
            var timeSinceFailure = DateTimeOffset.UtcNow - _lastFailure;
            if (timeSinceFailure < _resetTimeout)
                throw new InvalidOperationException("Circuit breaker is open");
            
            _state = CircuitState.HalfOpen;
        }

        try
        {
            var result = await operation();

            if (result.Success)
            {
                _failureCount = 0;
                _state = CircuitState.Closed;
                return result;
            }

            _failureCount++;
            if (_failureCount >= _failureThreshold)
            {
                _state = CircuitState.Open;
                _lastFailure = DateTimeOffset.UtcNow;
            }

            return result;
        }
        catch (Exception ex)
        {
            _failureCount++;
            if (_failureCount >= _failureThreshold)
            {
                _state = CircuitState.Open;
                _lastFailure = DateTimeOffset.UtcNow;
            }
            throw;
        }
    }
}
```

### Bulkhead Pattern

Limit concurrent requests:

```csharp
public class BulkheadExecutor
{
    private readonly SemaphoreSlim _semaphore;

    public BulkheadExecutor(int maxConcurrent = 10)
    {
        _semaphore = new SemaphoreSlim(maxConcurrent);
    }

    public async Task<AiCompletionResult> Execute(
        Func<Task<AiCompletionResult>> operation)
    {
        await _semaphore.WaitAsync();
        try
        {
            return await operation();
        }
        finally
        {
            _semaphore.Release();
        }
    }
}

// Usage
var executor = new BulkheadExecutor(maxConcurrent: 5);
var tasks = new List<Task<AiCompletionResult>>();

foreach (var item in items)
{
    tasks.Add(executor.Execute(() => client.SendAsync($"Process {item}")));
}

var results = await Task.WhenAll(tasks);
```

---

## Timeout Configuration

### HttpClient Timeout

Configure in DI:

```csharp
builder.Services.AddHttpClient<AnthropicClient>()
    .ConfigureHttpClient(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(60);  // 60 second timeout
    });
```

### Per-Request Timeout

```csharp
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

try
{
    var result = await client.SendAsync("prompt", cancellationToken: cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Request timed out after 30 seconds");
}
```

### Adaptive Timeout

Increase timeout for longer requests:

```csharp
public async Task<AiCompletionResult> SendWithAdaptiveTimeout(
    string prompt, string? systemPrompt = null)
{
    // Estimate time needed based on prompt size
    var timeoutSeconds = Math.Max(30, (prompt.Length / 1000) * 5);
    var cts = new CancellationTokenSource(
        TimeSpan.FromSeconds(timeoutSeconds));

    return await client.SendAsync(prompt, systemPrompt, 
        cancellationToken: cts.Token);
}
```

---

## Connection Pooling

### HttpClient Reuse

✅ **Correct**: Reuse single HttpClient
```csharp
// Good: Reuse HttpClient across requests
public class Service
{
    private readonly AnthropicClient _client;

    public Service(AnthropicClient client)
    {
        _client = client;  // Injected, reused
    }
}
```

❌ **Incorrect**: Create new HttpClient per request
```csharp
// Bad: Creates new HttpClient each time
for (int i = 0; i < 100; i++)
{
    var client = new HttpClient();  // ❌ Don't do this
    var result = await client.GetAsync("...");
}
```

### Connection Limits

```csharp
var handler = new SocketsHttpHandler
{
    MaxConnectionsPerServer = 50,  // Limit concurrent connections
    PooledConnectionLifetime = TimeSpan.FromMinutes(5)  // Recycle connections
};

var httpClient = new HttpClient(handler);
```

---

## Cost Optimization

### Cost Comparison

As of 2025:
- **Anthropic Claude**: ~$3/M input, $15/M output tokens
- **Gemini**: Free tier available, paid ~$0.075/M input, $0.3/M output

Use Gemini for:
- Simple tasks (summarization, classification)
- Prototyping and testing
- High volume, cost-sensitive workloads

Use Anthropic for:
- Complex reasoning
- Long-context tasks
- Production-critical features

### Caching Responses

```csharp
public class CachedAiService
{
    private readonly Dictionary<string, string> _cache = new();
    private readonly AnthropicClient _client;

    public async Task<string> GetResponse(string prompt)
    {
        if (_cache.TryGetValue(prompt, out var cached))
            return cached;

        var result = await _client.SendAsync(prompt);
        
        if (result.Success)
            _cache[prompt] = result.Content;

        return result.Content;
    }
}
```

### Batch Processing

Process multiple items in one session:

```csharp
public async Task<List<string>> ProcessBatch(List<string> items)
{
    var results = new List<string>();
    var batch = "";

    foreach (var item in items)
    {
        batch += $"- {item}\n";

        if (batch.Length > 10000)  // Process in chunks
        {
            var result = await client.SendAsync($"Process:\n{batch}");
            results.AddRange(ParseResults(result.Content));
            batch = "";
        }
    }

    if (!string.IsNullOrEmpty(batch))
    {
        var result = await client.SendAsync($"Process:\n{batch}");
        results.AddRange(ParseResults(result.Content));
    }

    return results;
}
```

---

## Monitoring

### Logging

```csharp
public class MonitoredAiService
{
    private readonly ILogger<MonitoredAiService> _logger;
    private readonly AnthropicClient _client;

    public MonitoredAiService(ILogger<MonitoredAiService> logger, AnthropicClient client)
    {
        _logger = logger;
        _client = client;
    }

    public async Task<string> Process(string prompt)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var result = await _client.SendAsync(prompt);

        stopwatch.Stop();

        if (result.Success)
        {
            _logger.LogInformation(
                "Request succeeded in {ElapsedMs}ms: {TokenCount} tokens",
                stopwatch.ElapsedMilliseconds,
                EstimateTokens(result.Content));
        }
        else
        {
            _logger.LogError(
                "Request failed after {ElapsedMs}ms: {Error}",
                stopwatch.ElapsedMilliseconds,
                result.Error);
        }

        return result.Content;
    }
}
```

### Metrics

Track key metrics:

```csharp
public class PerformanceMetrics
{
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public int RateLimitCount { get; set; }
    public long TotalTokens { get; set; }
    public long TotalDuration { get; set; }

    public double AverageDurationMs => TotalDuration / (double)(SuccessCount + FailureCount);
    public double SuccessRate => SuccessCount / (double)(SuccessCount + FailureCount);
}
```

---

See [TROUBLESHOOTING.md](TROUBLESHOOTING.md) for common issues, or [ARCHITECTURE.md](ARCHITECTURE.md) for rate limiting philosophy.
