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
    public void AddAiProviders_RegistersResolvableTypedClients()
    {
        var services = new ServiceCollection();
        services.AddAiProviders(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<AnthropicClient>().ShouldNotBeNull();
        provider.GetRequiredService<GeminiClient>().ShouldNotBeNull();
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
}
