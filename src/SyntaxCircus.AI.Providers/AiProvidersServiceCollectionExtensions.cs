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
    /// <remarks>
    /// Each typed <see cref="HttpClient"/> is wrapped in <c>SyntaxCircus.Http.Resilience</c>'s
    /// retry + circuit-breaker pipeline (<c>aiMode: true</c>) so transport failures and 5xx
    /// responses are retried automatically. HTTP 429 is deliberately excluded from that
    /// automatic retry: all three clients already surface rate limiting to the caller via
    /// <see cref="AiCompletionResult.IsRateLimited"/> and <see cref="RetryAfterParser"/>, so
    /// letting the resilience pipeline also retry 429s would fight that caller-visible backoff
    /// contract instead of complementing it.
    /// </remarks>
    public static IServiceCollection AddAiProviders(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<AnthropicClientOptions>(configuration.GetSection(AnthropicClientOptions.SectionName));
        services.Configure<GeminiClientOptions>(configuration.GetSection(GeminiClientOptions.SectionName));
        services.Configure<OpenAiCompatibleClientOptions>(configuration.GetSection(OpenAiCompatibleClientOptions.SectionName));

        services.AddResilientHttpClient(
                nameof(AnthropicClient),
                client => client.BaseAddress = new Uri("https://api.anthropic.com/"),
                retryCount: 3,
                aiMode: true)
            .AddTypedClient<AnthropicClient>();

        services.AddResilientHttpClient(
                nameof(GeminiClient),
                client => client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/"),
                retryCount: 3,
                aiMode: true)
            .AddTypedClient<GeminiClient>();

        services.AddResilientHttpClient(
                nameof(OpenAiCompatibleClient),
                retryCount: 3,
                aiMode: true)
            .AddTypedClient<OpenAiCompatibleClient>();

        return services;
    }
}
