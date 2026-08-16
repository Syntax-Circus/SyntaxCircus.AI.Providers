namespace SyntaxCircus.AI.Providers;

public sealed class AnthropicClientOptions
{
    public const string SectionName = "Anthropic";

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "claude-sonnet-5";

    public int MaxTokens { get; set; } = 4096;
}
