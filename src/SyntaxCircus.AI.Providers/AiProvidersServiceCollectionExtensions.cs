namespace SyntaxCircus.AI.Providers;

public static class AiProvidersServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="AnthropicClientOptions"/> / <see cref="GeminiClientOptions"/> (bound
    /// from the "Anthropic" / "Gemini" sections) and typed <see cref="HttpClient"/>s for
    /// <see cref="AnthropicClient"/> and <see cref="GeminiClient"/>.
    /// </summary>
    public static IServiceCollection AddAiProviders(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<AnthropicClientOptions>(configuration.GetSection(AnthropicClientOptions.SectionName));
        services.Configure<GeminiClientOptions>(configuration.GetSection(GeminiClientOptions.SectionName));

        services.AddHttpClient<AnthropicClient>(client => client.BaseAddress = new Uri("https://api.anthropic.com/"));
        services.AddHttpClient<GeminiClient>(client => client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/"));

        return services;
    }
}
