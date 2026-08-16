# Troubleshooting Guide

Common issues and solutions.

## Table of Contents
- [Authentication Issues](#authentication-issues)
- [API Errors](#api-errors)
- [Schema Validation Issues](#schema-validation-issues)
- [Performance Issues](#performance-issues)
- [Debugging Tips](#debugging-tips)

---

## Authentication Issues

### "Unauthorized (401)" or "Invalid API Key"

**Problem**: Request fails with 401 Unauthorized.

**Solution**:
1. Verify API key is correct
   ```bash
   echo $ANTHROPIC_API_KEY  # Check env var
   ```
2. Ensure key is set in configuration
   ```json
   {
     "Anthropic": {
       "ApiKey": "sk-ant-..."  // Should not be empty
     }
   }
   ```
3. Verify key has appropriate permissions (check provider dashboard)
4. Ensure key is not expired (some providers require rotation)

**Example Debugging**:
```csharp
var apiKey = _options.Value.ApiKey;
if (string.IsNullOrEmpty(apiKey))
    throw new InvalidOperationException("API key not configured");

_logger.LogInformation("Using API key: {KeyStart}...", apiKey.Substring(0, 10));
```

### "Invalid Authentication Method"

**Problem**: Different provider requires different auth header format.

**Solution**: Check provider docs:
- Anthropic: `x-api-key: sk-ant-...`
- Gemini: `Authorization: Bearer ...` (or key in URL)

The package handles this automatically, so if you see this error, likely the key format is wrong for the provider.

---

## API Errors

### "429 - Too Many Requests" (Rate Limited)

**Problem**: Request fails with 429 status code.

**Solution**:
1. Check `result.IsRateLimited` property
   ```csharp
   if (result.IsRateLimited)
   {
       // Wait before retrying
       await Task.Delay(TimeSpan.FromSeconds(5));
       result = await client.SendAsync(prompt);
   }
   ```
2. Implement exponential backoff (see [PERFORMANCE.md](PERFORMANCE.md))
3. Check rate limit plan:
   - Anthropic free: ~50k tokens/min
   - Gemini free: ~60 requests/min
4. Consider batching requests or upgrading plan

### "500 - Internal Server Error"

**Problem**: Provider API is returning 500 error.

**Solution**:
1. This is not your problem—provider has an issue
2. Retry with backoff:
   ```csharp
   const int maxRetries = 3;
   for (int i = 0; i < maxRetries; i++)
   {
       var result = await client.SendAsync(prompt);
       if (result.Success || !result.Error.Contains("500"))
           return result;
       await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, i)));
   }
   ```
3. Check provider status page
4. Try again in a few minutes

### "408 - Request Timeout"

**Problem**: Request times out before getting response.

**Solution**:
1. Increase timeout:
   ```csharp
   var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
   var result = await client.SendAsync(prompt, cancellationToken: cts.Token);
   ```
2. Check your network connection
3. Try with shorter prompt (might be faster)
4. Try later (provider might be slow)

### "Invalid JSON in Response"

**Problem**: Provider returns malformed JSON.

**Solution**:
1. This usually means provider had an issue
2. Check error message for details
3. Retry the request
4. If persists, contact provider support

---

## Schema Validation Issues

### "Response does not match schema"

**Problem**: Response validation fails when using `responseJsonSchema`.

**Causes & Solutions**:

**1. Schema is structurally invalid**
```csharp
// ❌ Invalid: missing type
var schema = """
{
  "properties": {
    "name": { "type": "string" }
  }
}
""";

// ✅ Valid: has type
var schema = """
{
  "type": "object",
  "properties": {
    "name": { "type": "string" }
  }
}
""";
```

**2. Response doesn't match schema structure**
```csharp
// ❌ Schema expects object with "result" property
var schema = """
{
  "type": "object",
  "properties": {
    "result": { "type": "string" }
  },
  "required": ["result"]
}
""";

// If response is just a string, it won't match

// ✅ Adjust prompt to request correct structure
var result = await client.SendAsync(
    prompt: "Return JSON: {\"result\": \"...value...\"}",
    responseJsonSchema: schema);
```

**3. Type mismatch in response**
```csharp
// Schema expects number
var schema = """
{
  "type": "object",
  "properties": {
    "count": { "type": "number" }
  }
}
""";

// If response has "count": "5" (string instead of number)
// it will fail validation

// Solution: Adjust schema or prompt to ensure correct type
var fixedSchema = """
{
  "type": "object",
  "properties": {
    "count": { "type": "integer" }
  }
}
""";
```

**4. Bypass validation for debugging**
```csharp
// ⚠️ Only for debugging—don't use in production
var result = await client.SendAsync(
    prompt: "...",
    responseJsonSchema: schema);

// Check actual response
Console.WriteLine(result.Content);
```

### "Invalid JSON Schema"

**Problem**: Schema itself is invalid.

**Solution**:
1. Use JSON Schema validator: https://www.jsonschemavalidator.com/
2. Ensure schema has required structure:
   ```json
   {
     "type": "object",
     "properties": { ... },
     "required": [...]  // Optional but recommended
   }
   ```
3. Test schema before using:
   ```csharp
   try
   {
       SchemaValidator.Validate(mySchema);
   }
   catch (ArgumentException ex)
   {
       Console.WriteLine($"Schema error: {ex.Message}");
   }
   ```

---

## Performance Issues

### "Requests are slow"

**Problem**: Individual requests take >10 seconds.

**Causes**:
- Network latency
- Provider is slow
- Large prompt/response (more tokens = more time)
- Too many concurrent requests (rate limiting)

**Solutions**:
1. Check response time:
   ```csharp
   var sw = System.Diagnostics.Stopwatch.StartNew();
   var result = await client.SendAsync(prompt);
   sw.Stop();
   Console.WriteLine($"Request took {sw.ElapsedMilliseconds}ms");
   ```

2. If network issue: check internet connection
3. If provider is slow: try different model or provider
4. If prompt is large: compress or split into smaller requests
5. If rate limiting: add delays between requests

### "Memory usage keeps growing"

**Problem**: Application memory usage increases over time.

**Causes**:
- Conversation history not being cleared
- Response cache growing unbounded
- Leaked HttpClient connections

**Solutions**:
1. Clear conversation history periodically:
   ```csharp
   _conversationHistory.Clear();  // Reset between sessions
   ```
2. Limit cache size:
   ```csharp
   if (_cache.Count > 1000)
       _cache.Clear();  // or implement LRU
   ```
3. Ensure HttpClient is reused (injected via DI)

### "High token usage"

**Problem**: Burning through tokens too quickly.

**Solutions** (see [PERFORMANCE.md](PERFORMANCE.md)):
1. Compress prompts (remove unnecessary context)
2. Use cheaper provider (Gemini) for simple tasks
3. Implement caching
4. Batch requests instead of one-by-one

---

## Debugging Tips

### Enable Logging

```csharp
var builder = WebApplicationBuilder.CreateBuilder(args);

// Log all requests
builder.Services.AddLogging(options =>
{
    options.AddConsole();
    options.AddDebug();
    options.SetMinimumLevel(LogLevel.Debug);  // Include HTTP details
});

// In appsettings.json:
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "System.Net.Http": "Debug"  // Log HTTP requests
    }
  }
}
```

### Log Responses

```csharp
var result = await client.SendAsync(prompt);

if (result.Success)
{
    _logger.LogInformation("Response: {Content}", result.Content);
}
else
{
    _logger.LogError(
        "Error: {Error}, IsRateLimit: {IsRateLimit}, IsTimeout: {IsTimeout}",
        result.Error,
        result.IsRateLimited,
        result.IsTimeout);
}
```

### Use Debugger

```csharp
var result = await client.SendAsync(prompt);

// Set breakpoint here to inspect result properties
// result.Success
// result.Content
// result.Error
// result.IsRateLimited
// result.IsTimeout
```

### Test with Simple Prompt

When debugging errors, start simple:

```csharp
// ✅ Start with basic request
var result = await client.SendAsync("Say hello");
if (!result.Success)
    throw new Exception($"Basic test failed: {result.Error}");

// ✅ Then add complexity (schema, history, etc.)
var withSchema = await client.SendAsync(
    "...", 
    responseJsonSchema: schema);
```

### Check Configuration

```csharp
var apiKey = _options.Value.ApiKey;
var model = _options.Value.Model;
var maxTokens = _options.Value.MaxTokens;

_logger.LogInformation(
    "Config: ApiKey={ApiKeyStart}..., Model={Model}, MaxTokens={MaxTokens}",
    apiKey?.Substring(0, 10) ?? "NOT SET",
    model,
    maxTokens);
```

### Network Debugging

```bash
# Check if API is reachable
curl -I https://api.anthropic.com/v1/messages

# Check DNS resolution
nslookup api.anthropic.com

# Test with curl (Linux/Mac)
curl -X POST https://api.anthropic.com/v1/messages \
  -H "x-api-key: $ANTHROPIC_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"model":"claude-opus-5","max_tokens":100,"messages":[{"role":"user","content":"Hi"}]}'
```

---

## Can't Find Your Issue?

1. Check [API_REFERENCE.md](API_REFERENCE.md) for all response codes
2. Search [EXAMPLES.md](EXAMPLES.md) for similar usage
3. Review [ARCHITECTURE.md](ARCHITECTURE.md) for design details
4. Check provider docs:
   - Anthropic: https://docs.anthropic.com/
   - Gemini: https://ai.google.dev/docs/

---

Still stuck? Open an issue on GitHub with:
- Which provider (Anthropic/Gemini)
- What you were trying to do
- Error message and stack trace
- Minimal reproduction code
