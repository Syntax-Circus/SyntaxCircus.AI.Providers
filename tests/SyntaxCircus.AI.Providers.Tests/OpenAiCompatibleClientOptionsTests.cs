namespace SyntaxCircus.AI.Providers.Tests;

public class OpenAiCompatibleClientOptionsTests
{
    [Fact]
    public void Defaults_MatchExpectedValues()
    {
        var options = new OpenAiCompatibleClientOptions();

        options.ApiKey.ShouldBe(string.Empty);
        options.Model.ShouldBe(string.Empty);
        options.BaseUrl.ShouldBe(string.Empty);
        options.MaxTokens.ShouldBe(4096);
    }

    [Fact]
    public void SectionName_IsOpenAiCompatible()
    {
        OpenAiCompatibleClientOptions.SectionName.ShouldBe("OpenAiCompatible");
    }
}
