# Contributing Guide

How to contribute to SyntaxCircus.AI.Providers.

## Table of Contents
- [Getting Started](#getting-started)
- [Code Style](#code-style)
- [Adding a New Provider](#adding-a-new-provider)
- [Testing Requirements](#testing-requirements)
- [Documentation Requirements](#documentation-requirements)
- [Pull Request Process](#pull-request-process)

---

## Getting Started

### Clone and Setup

```bash
git clone https://github.com/Syntax-Circus/SyntaxCircus.AI.Providers.git
cd SyntaxCircus.AI.Providers
dotnet build
dotnet test
```

### Project Structure

```
src/
├── SyntaxCircus.AI.Providers/
│   ├── AnthropicClient.cs          # Anthropic provider
│   ├── GeminiClient.cs              # Gemini provider
│   ├── AiCompletionResult.cs        # Shared response type
│   ├── AiChatMessage.cs             # Chat message type
│   ├── SchemaValidator.cs           # Schema validation utility
│   └── ...options and DTOs
tests/
├── SyntaxCircus.AI.Providers.Tests/
│   ├── AnthropicClientTests.cs
│   ├── GeminiClientTests.cs
│   ├── SchemaValidatorTests.cs
│   └── ...other tests
docs/
├── GETTING_STARTED.md
├── API_REFERENCE.md
├── ...11 other docs
```

---

## Code Style

See `.editorconfig` for formatting rules. Key conventions:

### Naming
- Classes: `PascalCase` (e.g., `AnthropicClient`)
- Methods: `PascalCase` (e.g., `SendAsync`)
- Parameters: `camelCase` (e.g., `responseJsonSchema`)
- Constants: `PascalCase` (e.g., `DefaultTimeout`)
- Private fields: `_camelCase` (e.g., `_httpClient`)

### Formatting
- Indentation: 4 spaces
- Line length: 120 characters (soft limit)
- Use `var` for obvious types, explicit types for clarity
- Braces on same line (K&R style)
- One blank line between methods

### Example

```csharp
public class AnthropicClient
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<AnthropicClientOptions> _options;

    public AnthropicClient(HttpClient httpClient, IOptions<AnthropicClientOptions> options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<AiCompletionResult> SendAsync(
        string prompt,
        string? systemPrompt = null,
        IReadOnlyList<AiChatMessage>? conversationHistory = null,
        string? responseJsonSchema = null,
        CancellationToken ct = default)
    {
        // Implementation
    }

    private void ValidateResponse(string content, string? schema)
    {
        // Private helper methods use PascalCase
    }
}
```

### Comments
- Only comment non-obvious logic
- Use `//` for single-line comments
- Use `///` for XML doc comments on public types/methods
- Example:

```csharp
/// <summary>
/// Sends a completion request to the API with optional schema validation.
/// </summary>
/// <param name="prompt">The user's message or prompt.</param>
/// <param name="responseJsonSchema">Optional JSON Schema for structured output (Anthropic only).</param>
/// <returns>Completion result with content or error.</returns>
public async Task<AiCompletionResult> SendAsync(string prompt, string? responseJsonSchema = null)
{
    // Check if schema is provided and needs validation
    if (!string.IsNullOrEmpty(responseJsonSchema))
    {
        SchemaValidator.Validate(responseJsonSchema);
    }

    // Send request...
}
```

---

## Adding a New Provider

### Step 1: Create the Client Class

Create `src/SyntaxCircus.AI.Providers/YourProviderClient.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace SyntaxCircus.AI.Providers;

/// <summary>
/// Client for YourProvider API integration.
/// </summary>
public sealed class YourProviderClient
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<YourProviderClientOptions> _options;
    private const string ApiEndpoint = "https://api.yourprovider.com/v1";

    public YourProviderClient(HttpClient httpClient, IOptions<YourProviderClientOptions> options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Sends a prompt and returns the completion result.
    /// </summary>
    public async Task<AiCompletionResult> SendAsync(
        string prompt,
        string? systemPrompt = null,
        IReadOnlyList<AiChatMessage>? conversationHistory = null,
        string? responseJsonSchema = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(prompt))
            throw new ArgumentException("Prompt cannot be empty", nameof(prompt));

        try
        {
            // Build request
            var request = BuildRequest(prompt, systemPrompt, conversationHistory, responseJsonSchema);

            // Send to API
            var response = await _httpClient.PostAsync($"{ApiEndpoint}/completions", request, ct);

            // Parse response
            var content = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
                return ParseErrorResponse(content);

            return ParseSuccessResponse(content, responseJsonSchema);
        }
        catch (HttpRequestException ex)
        {
            return new AiCompletionResult($"API request failed: {ex.Message}");
        }
        catch (JsonException ex)
        {
            return new AiCompletionResult($"Failed to parse API response: {ex.Message}");
        }
    }

    private HttpContent BuildRequest(
        string prompt, string? systemPrompt, 
        IReadOnlyList<AiChatMessage>? conversationHistory,
        string? responseJsonSchema)
    {
        // TODO: Build provider-specific request format
        var requestBody = new { prompt, systemPrompt };
        var json = JsonSerializer.Serialize(requestBody);
        return new StringContent(json, System.Text.Encoding.UTF8, "application/json");
    }

    private AiCompletionResult ParseSuccessResponse(
        string responseContent, string? schema, bool skipValidation)
    {
        try
        {
            var json = JsonSerializer.Deserialize<JsonElement>(responseContent);
            var text = json.GetProperty("text").GetString() ?? "";

            if (!string.IsNullOrEmpty(schema) && !skipValidation)
            {
                if (!SchemaValidator.Validate(text, schema))
                    return new AiCompletionResult($"Response does not match schema");
            }

            return new AiCompletionResult(text);
        }
        catch (Exception ex)
        {
            return new AiCompletionResult($"Failed to parse response: {ex.Message}");
        }
    }

    private AiCompletionResult ParseErrorResponse(string content)
    {
        try
        {
            var json = JsonSerializer.Deserialize<JsonElement>(content);
            var error = json.TryGetProperty("error", out var errorProp) 
                ? errorProp.GetString() 
                : content;
            return new AiCompletionResult(error ?? "Unknown error");
        }
        catch
        {
            return new AiCompletionResult(content);
        }
    }
}

/// <summary>
/// Configuration options for YourProviderClient.
/// </summary>
public sealed class YourProviderClientOptions
{
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "default-model";
    public int MaxTokens { get; set; } = 4096;
}
```

### Step 2: Create Options Class

Already shown in Step 1 (`YourProviderClientOptions`).

### Step 3: Create Tests

Create `tests/SyntaxCircus.AI.Providers.Tests/YourProviderClientTests.cs`:

```csharp
using Xunit;
using Shouldly;
using SyntaxCircus.AI.Providers;
using System.Net;

namespace SyntaxCircus.AI.Providers.Tests;

public class YourProviderClientTests
{
    [Fact]
    public async Task SendAsync_WithValidPrompt_ReturnsSuccess()
    {
        // Arrange
        var responseJson = """{"text":"Test response","usage":{"tokens":10}}""";
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(handler);
        var options = Options.Create(new YourProviderClientOptions 
        { 
            ApiKey = "test-key",
            Model = "test-model"
        });
        var client = new YourProviderClient(httpClient, options);

        // Act
        var result = await client.SendAsync("Test prompt");

        // Assert
        result.Success.ShouldBeTrue();
        result.Content.ShouldContain("Test response");
    }

    [Fact]
    public async Task SendAsync_WithEmptyPrompt_ThrowsArgumentException()
    {
        // Arrange
        var httpClient = new HttpClient();
        var options = Options.Create(new YourProviderClientOptions { ApiKey = "test" });
        var client = new YourProviderClient(httpClient, options);

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(() => client.SendAsync(""));
    }

    // ... more tests for error handling, schema validation, etc.
}
```

### Step 4: Register in DI

Update `src/SyntaxCircus.AI.Providers/ServiceCollectionExtensions.cs` to register your client:

```csharp
public static IServiceCollection AddAiProviders(
    this IServiceCollection services, IConfiguration configuration)
{
    services.Configure<AnthropicClientOptions>(configuration.GetSection("Anthropic"));
    services.Configure<GeminiClientOptions>(configuration.GetSection("Gemini"));
    services.Configure<YourProviderClientOptions>(configuration.GetSection("YourProvider"));  // Add this

    services.AddHttpClient<AnthropicClient>();
    services.AddHttpClient<GeminiClient>();
    services.AddHttpClient<YourProviderClient>();  // Add this

    return services;
}
```

### Step 5: Update Documentation

- Add examples to `docs/EXAMPLES.md`
- Add API section to `docs/API_REFERENCE.md`
- Update `docs/INDEX.md` if needed

---

## Testing Requirements

Every change must include tests:

### Unit Tests
- Test happy path
- Test error handling (network, API errors)
- Test validation logic
- Mock HTTP responses
- Minimum 80% coverage for new code

### Integration Tests
- Test with real API (if possible)
- Mark with `[Trait("Category", "Integration")]`
- Should be skipped in CI unless explicitly enabled

### Running Tests

```bash
# Run all tests
dotnet test

# Run specific test class
dotnet test --filter "ClassName=YourProviderClientTests"

# Run with coverage
dotnet test /p:CollectCoverage=true
```

---

## Documentation Requirements

Every feature needs documentation:

1. **Code comments**: Non-obvious logic only
2. **XML docs**: Public types/methods with `///`
3. **README.md**: Link to relevant docs
4. **Docs file**: Add to appropriate guide (EXAMPLES.md, API_REFERENCE.md, etc.)
5. **RELEASE_NOTES.md**: If user-facing

### Documentation Template

```markdown
## Your Feature

### Overview
Brief description of what this does and why.

### Usage
Copy-paste ready example:

\`\`\`csharp
var client = new YourProviderClient(...);
var result = await client.SendAsync("prompt");
\`\`\`

### Error Handling
How errors are returned and how to handle them.

### Limitations
Any gotchas or known issues.

### Related
Links to other relevant docs.
```

---

## Pull Request Process

1. **Create a branch**: `git checkout -b feature/your-feature`
2. **Make changes**: Follow code style, add tests, update docs
3. **Run tests**: `dotnet test` (must pass)
4. **Run linter** (if applicable): Check `.editorconfig` compliance
5. **Commit**: Use clear, descriptive commit messages
6. **Push**: `git push origin feature/your-feature`
7. **Create PR**: Link to any relevant issues
8. **Request review**: Wait for maintainer approval

### PR Checklist
- ✅ Tests added and passing
- ✅ Documentation updated
- ✅ No breaking changes (or clearly documented)
- ✅ Code follows style guide
- ✅ RELEASE_NOTES.md updated if user-facing

### Commit Message Template

```
[TYPE] Brief description (50 chars max)

Longer explanation if needed (wrap at 72 chars).
Mention why this change is needed.

Fixes #123
```

Types: `feat`, `fix`, `docs`, `test`, `refactor`, `perf`

Example:
```
feat: Add schema validation for Anthropic responses

Added SchemaValidator class to validate JSON responses
against provided JSON Schema. Included client-side 
validation to fail fast before returning to user.

Fixes #15
```

---

## Questions?

See [ARCHITECTURE.md](ARCHITECTURE.md) for design philosophy or check existing tests for patterns.
