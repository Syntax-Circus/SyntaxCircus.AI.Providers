namespace SyntaxCircus.AI.Providers.Tests;

public class RetryAfterParserTests
{
    [Fact]
    public void Parse_WithDeltaSecondsHeader_ReturnsUtcNowPlusDelta()
    {
        using var response = new HttpResponseMessage();
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(30));

        var before = DateTimeOffset.UtcNow;
        var result = RetryAfterParser.Parse(response);
        var after = DateTimeOffset.UtcNow;

        result.ShouldNotBeNull();
        result.Value.ShouldBeInRange(before.AddSeconds(30), after.AddSeconds(30));
    }

    [Fact]
    public void Parse_WithAbsoluteDateHeader_ReturnsThatDate()
    {
        var expected = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        using var response = new HttpResponseMessage();
        response.Headers.RetryAfter = new RetryConditionHeaderValue(expected);

        var result = RetryAfterParser.Parse(response);

        result.ShouldBe(expected);
    }

    [Fact]
    public void Parse_WithNoRetryAfterHeader_ReturnsNull()
    {
        using var response = new HttpResponseMessage();

        var result = RetryAfterParser.Parse(response);

        result.ShouldBeNull();
    }

    [Fact]
    public void Parse_WithNullResponse_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => RetryAfterParser.Parse(null!));
    }
}
