namespace SyntaxCircus.AI.Providers.Tests;

public class GeminiClientOptionsTests
{
    [Fact]
    public void Defaults_MatchExpectedValues()
    {
        var options = new GeminiClientOptions();

        options.ApiKey.ShouldBe(string.Empty);
        options.Model.ShouldBe("gemini-2.5-flash");
        options.MaxOutputTokens.ShouldBe(4096);
    }

    [Fact]
    public void SectionName_IsGemini()
    {
        GeminiClientOptions.SectionName.ShouldBe("Gemini");
    }
}
