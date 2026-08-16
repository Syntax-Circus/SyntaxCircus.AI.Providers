namespace SyntaxCircus.AI.Providers.Tests;

public class AiCompletionResultTests
{
    [Fact]
    public void Success_WhenErrorIsNull_IsTrue()
    {
        var result = new AiCompletionResult("hello");

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public void Success_WhenErrorIsSet_IsFalse()
    {
        var result = new AiCompletionResult(string.Empty, Error: "boom");

        result.Success.ShouldBeFalse();
    }
}
