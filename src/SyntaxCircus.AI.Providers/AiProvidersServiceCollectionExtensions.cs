namespace SyntaxCircus.AI.Providers;

public static class AiProvidersServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="AnthropicClientOptions"/> / <see cref="GeminiClientOptions"/> /
    /// <see cref="OpenAiCompatibleClientOptions"/> (bound from the "Anthropic" / "Gemini" /
    /// "OpenAiCompatible" sections) and typed <see cref="HttpClient"/>s for
    /// <see cref="AnthropicClient"/>, <see cref="GeminiClient"/>, and
    /// <see cref="OpenAiCompatibleClient"/>. Unlike the other two, <see cref="OpenAiCompatibleClient"/>'s
    /// <see cref="HttpClient"/> is registered without a fixed <c>BaseAddress</c> — its host is
    /// resolved per call, since it targets a runtime-variable endpoint rather than a single
    /// well-known one.
    /// </summary>
    public static IServiceCollection AddAiProviders(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<AnthropicClientOptions>(configuration.GetSection(AnthropicClientOptions.SectionName));
        services.Configure<GeminiClientOptions>(configuration.GetSection(GeminiClientOptions.SectionName));
        services.Configure<OpenAiCompatibleClientOptions>(configuration.GetSection(OpenAiCompatibleClientOptions.SectionName));

        services.AddHttpClient<AnthropicClient>(client => client.BaseAddress = new Uri("https://api.anthropic.com/"));
        services.AddHttpClient<GeminiClient>(client => client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/"));
        services.AddHttpClient<OpenAiCompatibleClient>();

        return services;
    }
}
