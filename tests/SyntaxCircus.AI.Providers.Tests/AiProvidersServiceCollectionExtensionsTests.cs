namespace SyntaxCircus.AI.Providers.Tests;

public class AiProvidersServiceCollectionExtensionsTests
{
    [Fact]
    public void AddAiProviders_RegistersAnthropicClientWithExpectedBaseAddress()
    {
        var services = new ServiceCollection();
        services.AddAiProviders(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        var factory = provider.GetRequiredService<IHttpClientFactory>();
        var httpClient = factory.CreateClient(nameof(AnthropicClient));

        httpClient.BaseAddress.ShouldBe(new Uri("https://api.anthropic.com/"));
    }

    [Fact]
    public void AddAiProviders_RegistersGeminiClientWithExpectedBaseAddress()
    {
        var services = new ServiceCollection();
        services.AddAiProviders(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        var factory = provider.GetRequiredService<IHttpClientFactory>();
        var httpClient = factory.CreateClient(nameof(GeminiClient));

        httpClient.BaseAddress.ShouldBe(new Uri("https://generativelanguage.googleapis.com/"));
    }

    [Fact]
    public void AddAiProviders_RegistersOpenAiCompatibleClientWithoutFixedBaseAddress()
    {
        var services = new ServiceCollection();
        services.AddAiProviders(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        var factory = provider.GetRequiredService<IHttpClientFactory>();
        var httpClient = factory.CreateClient(nameof(OpenAiCompatibleClient));

        httpClient.BaseAddress.ShouldBeNull();
    }

    [Fact]
    public void AddAiProviders_RegistersResolvableTypedClients()
    {
        var services = new ServiceCollection();
        services.AddAiProviders(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<AnthropicClient>().ShouldNotBeNull();
        provider.GetRequiredService<GeminiClient>().ShouldNotBeNull();
        provider.GetRequiredService<OpenAiCompatibleClient>().ShouldNotBeNull();
    }

    [Fact]
    public void AddAiProviders_BindsOptionsFromConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Anthropic:ApiKey"] = "anthropic-key",
                ["Anthropic:Model"] = "claude-x",
                ["Gemini:ApiKey"] = "gemini-key",
                ["OpenAiCompatible:ApiKey"] = "openai-compatible-key",
                ["OpenAiCompatible:BaseUrl"] = "https://openrouter.ai/api/v1/",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddAiProviders(configuration);
        using var provider = services.BuildServiceProvider();

        var anthropicOptions = provider.GetRequiredService<IOptions<AnthropicClientOptions>>().Value;
        anthropicOptions.ApiKey.ShouldBe("anthropic-key");
        anthropicOptions.Model.ShouldBe("claude-x");

        var geminiOptions = provider.GetRequiredService<IOptions<GeminiClientOptions>>().Value;
        geminiOptions.ApiKey.ShouldBe("gemini-key");

        var openAiCompatibleOptions = provider.GetRequiredService<IOptions<OpenAiCompatibleClientOptions>>().Value;
        openAiCompatibleOptions.ApiKey.ShouldBe("openai-compatible-key");
        openAiCompatibleOptions.BaseUrl.ShouldBe("https://openrouter.ai/api/v1/");
    }

    [Fact]
    public void AddAiProviders_WithNullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;

        Should.Throw<ArgumentNullException>(() => services.AddAiProviders(new ConfigurationBuilder().Build()));
    }

    [Fact]
    public void AddAiProviders_WithNullConfiguration_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(() => services.AddAiProviders(null!));
    }

    [Theory]
    [InlineData(nameof(AnthropicClient))]
    [InlineData(nameof(GeminiClient))]
    [InlineData(nameof(OpenAiCompatibleClient))]
    public async Task AddAiProviders_TypedClient_RetriesTransientServerErrorsUntilSuccessful(string clientName)
    {
        var attempt = 0;
        var handler = new StubHttpMessageHandler(_ =>
        {
            attempt++;
            return attempt < 3
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : new HttpResponseMessage(HttpStatusCode.OK);
        });

        var services = new ServiceCollection();
        services.AddAiProviders(new ConfigurationBuilder().Build());
        // HttpClientFactory registrations for the same name are additive, so this attaches a stub
        // primary handler underneath the resilience pipeline AddAiProviders already registered
        // above, without having to restructure AddAiProviders itself for testability.
        services.AddHttpClient(clientName).ConfigurePrimaryHttpMessageHandler(() => handler);
        using var provider = services.BuildServiceProvider();

        var factory = provider.GetRequiredService<IHttpClientFactory>();
        var httpClient = factory.CreateClient(clientName);

        using var response = await httpClient.GetAsync(
            new Uri("https://example.test/probe"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        handler.CallCount.ShouldBe(3);
    }

    [Theory]
    [InlineData(nameof(AnthropicClient))]
    [InlineData(nameof(GeminiClient))]
    [InlineData(nameof(OpenAiCompatibleClient))]
    public async Task AddAiProviders_TypedClient_DoesNotAutoRetryTooManyRequests(string clientName)
    {
        // aiMode: true excludes 429 from the resilience pipeline's automatic retry, since all
        // three typed clients already surface rate limiting to the caller themselves (see
        // AiCompletionResult.IsRateLimited / RetryAfterParser) rather than relying on this layer.
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests));

        var services = new ServiceCollection();
        services.AddAiProviders(new ConfigurationBuilder().Build());
        services.AddHttpClient(clientName).ConfigurePrimaryHttpMessageHandler(() => handler);
        using var provider = services.BuildServiceProvider();

        var factory = provider.GetRequiredService<IHttpClientFactory>();
        var httpClient = factory.CreateClient(clientName);

        using var response = await httpClient.GetAsync(
            new Uri("https://example.test/probe"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        handler.CallCount.ShouldBe(1);
    }
}
