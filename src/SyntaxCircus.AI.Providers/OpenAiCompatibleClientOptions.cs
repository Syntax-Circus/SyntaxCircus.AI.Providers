namespace SyntaxCircus.AI.Providers;

public sealed class OpenAiCompatibleClientOptions
{
    public const string SectionName = "OpenAiCompatible";

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = string.Empty;

    public int MaxTokens { get; set; } = 4096;
}
