# Integration Guide

How to integrate SyntaxCircus.AI.Providers into your projects.

## Table of Contents
- [Installation](#installation)
- [Configuration](#configuration)
- [Dependency Injection](#dependency-injection)
- [Common Scenarios](#common-scenarios)
- [Testing](#testing)

---

## Installation

### NuGet Package Manager

```bash
dotnet add package SyntaxCircus.AI.Providers
```

### Package Manager Console

```powershell
Install-Package SyntaxCircus.AI.Providers
```

### .csproj

```xml
<ItemGroup>
  <PackageReference Include="SyntaxCircus.AI.Providers" Version="1.0.0" />
</ItemGroup>
```

---

## Configuration

### 1. Set API Keys

Get API keys from:
- **Anthropic**: https://console.anthropic.com/
- **Gemini**: https://aistudio.google.com/apikey

### 2. Store in appsettings.json

```json
{
  "Anthropic": {
    "ApiKey": "sk-ant-...",
    "Model": "claude-opus-5",
    "MaxTokens": 4096
  },
  "Gemini": {
    "ApiKey": "your-api-key",
    "Model": "gemini-2.5-flash",
    "MaxOutputTokens": 4096
  }
}
```

### 3. Secure API Keys

**Development**:
```bash
dotnet user-secrets init
dotnet user-secrets set "Anthropic:ApiKey" "sk-ant-..."
dotnet user-secrets set "Gemini:ApiKey" "..."
```

**Production** (Azure Key Vault):
```csharp
var keyVaultUrl = "https://mykeyvault.vault.azure.net/";
builder.Configuration.AddAzureKeyVault(
    new Uri(keyVaultUrl),
    new DefaultAzureCredential());
```

---

## Dependency Injection

### Basic Setup

Add to your `Program.cs`:

```csharp
using SyntaxCircus.AI.Providers;

var builder = WebApplicationBuilder.CreateBuilder(args);

// Register AI providers
builder.Services.AddAiProviders(builder.Configuration);

// Add your services that use the clients
builder.Services.AddScoped<SummarizeService>();

var app = builder.Build();
```

### What Gets Registered

- `AnthropicClient` → scoped service
- `GeminiClient` → scoped service
- Configuration bindings for both

### Accessing in Controllers

```csharp
[ApiController]
[Route("api/[controller]")]
public class SummarizeController
{
    private readonly AnthropicClient _anthropic;

    public SummarizeController(AnthropicClient anthropic)
    {
        _anthropic = anthropic;
    }

    [HttpPost]
    public async Task<IActionResult> Summarize([FromBody] string text)
    {
        var result = await _anthropic.SendAsync($"Summarize: {text}");
        
        if (!result.Success)
            return BadRequest(result.Error);
        
        return Ok(result.Content);
    }
}
```

### Accessing in Services

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
        var result = await _client.SendAsync($"Summarize: {text}");
        return result.Success ? result.Content : throw new Exception(result.Error);
    }
}
```

---

## Common Scenarios

### Console Application

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SyntaxCircus.AI.Providers;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddUserSecrets<Program>()
    .Build();

var services = new ServiceCollection();
services.AddAiProviders(config);

var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<AnthropicClient>();

var result = await client.SendAsync("Hello!");
Console.WriteLine(result.Content);
```

### Worker Service

```csharp
using Microsoft.Extensions.Hosting;
using SyntaxCircus.AI.Providers;

Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddAiProviders(context.Configuration);
        services.AddHostedService<ProcessingWorker>();
    })
    .Build()
    .Run();

public class ProcessingWorker : BackgroundService
{
    private readonly AnthropicClient _client;

    public ProcessingWorker(AnthropicClient client)
    {
        _client = client;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var result = await _client.SendAsync("Process this");
            // Do work
            await Task.Delay(1000, stoppingToken);
        }
    }
}
```

### Azure Function

```csharp
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SyntaxCircus.AI.Providers;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddAiProviders(hostContext.Configuration);
    })
    .Build();

host.Run();

public class ProcessFunction
{
    private readonly AnthropicClient _client;

    public ProcessFunction(AnthropicClient client)
    {
        _client = client;
    }

    [Function("Process")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequestData req)
    {
        var result = await _client.SendAsync("Process request");
        
        var response = req.CreateResponse(
            result.Success ? System.Net.HttpStatusCode.OK : System.Net.HttpStatusCode.InternalServerError);
        response.WriteAsJsonAsync(new { content = result.Content, error = result.Error });
        return response;
    }
}
```

---

## Testing

### Unit Test Setup

```csharp
using Microsoft.Extensions.Options;
using Shouldly;

public class MyServiceTests
{
    private class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public HttpRequestMessage? LastRequest { get; private set; }

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_handler(request));
        }
    }

    [Fact]
    public async Task MyService_WithValidResponse_Succeeds()
    {
        // Arrange
        var responseJson = """{"content":[{"text":"Test response"}],"usage":{"output_tokens":10}}""";
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/") };
        var options = Options.Create(new AnthropicClientOptions
        {
            ApiKey = "test-key",
            Model = "test-model",
            MaxTokens = 1024
        });

        var client = new AnthropicClient(httpClient, options);
        var service = new MyService(client);

        // Act
        var result = await service.DoSomething("input");

        // Assert
        result.ShouldBe("Test response");
    }
}
```

### Integration Test

```csharp
[Fact]
[Trait("Category", "Integration")]
public async Task WithRealApi_Succeeds()
{
    // Requires real API key
    var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
        ?? throw new Exception("ANTHROPIC_API_KEY not set");

    var httpClient = new HttpClient();
    var options = Options.Create(new AnthropicClientOptions
    {
        ApiKey = apiKey,
        Model = "claude-opus-5",
        MaxTokens = 1024
    });

    var client = new AnthropicClient(httpClient, options);
    var result = await client.SendAsync("Say hello");

    result.Success.ShouldBeTrue();
    result.Content.ShouldNotBeEmpty();
}
```

### Mocking in Tests

```csharp
// Option 1: Mock the client itself
var mockClient = new Mock<AnthropicClient>();
mockClient
    .Setup(c => c.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<AiChatMessage>>(), 
        It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
    .ReturnsAsync(new AiCompletionResult("Mock response"));

var service = new MyService(mockClient.Object);
var result = await service.DoSomething();

// Option 2: Inject a fake implementation
public class FakeAnthropicClient : AnthropicClient
{
    public FakeAnthropicClient() : base(new HttpClient(), Options.Create(
        new AnthropicClientOptions { ApiKey = "test", Model = "test", MaxTokens = 1024 }))
    {
    }

    public override async Task<AiCompletionResult> SendAsync(
        string prompt, string? systemPrompt = null, 
        IReadOnlyList<AiChatMessage>? conversationHistory = null,
        string? responseJsonSchema = null,
        CancellationToken ct = default)
    {
        return new AiCompletionResult("Fake response");
    }
}
```

---

See [GETTING_STARTED.md](GETTING_STARTED.md) for quick start, or [DESIGN_PATTERNS.md](DESIGN_PATTERNS.md) for common patterns.
