namespace SyntaxCircus.AI.Providers;

public sealed class GeminiClientOptions
{
    public const string SectionName = "Gemini";

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "gemini-2.5-flash";

    public int MaxOutputTokens { get; set; } = 4096;
}
