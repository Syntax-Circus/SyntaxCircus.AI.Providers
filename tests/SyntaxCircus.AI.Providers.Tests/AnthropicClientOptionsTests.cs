namespace SyntaxCircus.AI.Providers.Tests;

public class AnthropicClientOptionsTests
{
    [Fact]
    public void Defaults_MatchExpectedValues()
    {
        var options = new AnthropicClientOptions();

        options.ApiKey.ShouldBe(string.Empty);
        options.Model.ShouldBe("claude-sonnet-5");
        options.MaxTokens.ShouldBe(4096);
    }

    [Fact]
    public void SectionName_IsAnthropic()
    {
        AnthropicClientOptions.SectionName.ShouldBe("Anthropic");
    }
}
