namespace SyntaxCircus.AI.Providers.Tests;

public class GeminiClientTests
{
    private static GeminiClient CreateClient(StubHttpMessageHandler handler, string apiKey = "test-key", string model = "gemini-2.5-flash")
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://generativelanguage.googleapis.com/") };
        var options = Options.Create(new GeminiClientOptions { ApiKey = apiKey, Model = model });
        return new GeminiClient(httpClient, options);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json) =>
        new(statusCode) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };

    [Fact]
    public async Task SendAsync_OnSuccess_ReturnsContentAndTokensUsed()
    {
        var handler = new StubHttpMessageHandler(_ =>
            JsonResponse(HttpStatusCode.OK, """{"candidates":[{"content":{"parts":[{"text":"Hi there"}]}}],"usageMetadata":{"totalTokenCount":7}}"""));
        var client = CreateClient(handler);

        var result = await client.SendAsync("hi", ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Content.ShouldBe("Hi there");
        result.TokensUsed.ShouldBe(7);
    }

    [Fact]
    public async Task SendAsync_WithNoApiKeyConfigured_ReturnsErrorWithoutSendingRequest()
    {
        var handler = new StubHttpMessageHandler(_ => throw new InvalidOperationException("Should not be called."));
        var client = CreateClient(handler, apiKey: string.Empty);

        var result = await client.SendAsync("hi", ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.Error.ShouldBe("Gemini API key is not configured.");
        handler.LastRequest.ShouldBeNull();
    }

    [Fact]
    public async Task SendAsync_On400_ReturnsInvalidRequestError()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));
        var client = CreateClient(handler);

        var result = await client.SendAsync("hi", ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.Error.ShouldBe("Invalid request.");
    }

    [Fact]
    public async Task SendAsync_On403_ReturnsInvalidApiKeyError()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));
        var client = CreateClient(handler);

        var result = await client.SendAsync("hi", ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.Error.ShouldBe("Invalid Gemini API key.");
    }

    [Fact]
    public async Task SendAsync_On429_ReturnsRateLimitedResultWithRetryAfter()
    {
        var handler = new StubHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(20));
            return response;
        });
        var client = CreateClient(handler);

        var result = await client.SendAsync("hi", ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.IsRateLimited.ShouldBeTrue();
        result.RetryAfter.ShouldNotBeNull();
        result.RetryAfter.Value.ShouldBeInRange(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddSeconds(20));
    }

    [Fact]
    public async Task SendAsync_WithNoCandidates_ReturnsContentFilteredError()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(HttpStatusCode.OK, """{"candidates":[]}"""));
        var client = CreateClient(handler);

        var result = await client.SendAsync("hi", ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.Error.ShouldBe("Content filtered by safety settings.");
    }

    [Fact]
    public async Task SendAsync_OnMalformedJson_ReturnsError()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(HttpStatusCode.OK, "not valid json"));
        var client = CreateClient(handler);

        var result = await client.SendAsync("hi", ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.Error.ShouldBe("Malformed response from Gemini API.");
    }

    [Fact]
    public async Task SendAsync_OnHttpRequestException_ReturnsHttpError()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("network down"));
        var client = CreateClient(handler);

        var result = await client.SendAsync("hi", ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.Error.ShouldBe("HTTP error: network down");
    }

    [Fact]
    public async Task SendAsync_SendsApiKeyViaHeaderNotQueryString()
    {
        var handler = new StubHttpMessageHandler(_ =>
            JsonResponse(HttpStatusCode.OK, """{"candidates":[{"content":{"parts":[{"text":"ok"}]}}]}"""));
        var client = CreateClient(handler, apiKey: "secret-key", model: "gemini-2.5-flash");

        await client.SendAsync("hi", ct: TestContext.Current.CancellationToken);

        handler.LastRequest.ShouldNotBeNull();
        handler.LastRequest.HeaderValue("x-goog-api-key").ShouldBe("secret-key");
        handler.LastRequest.RequestUri!.ToString().ShouldNotContain("secret-key");
        handler.LastRequest.RequestUri!.ToString().ShouldContain("v1beta/models/gemini-2.5-flash:generateContent");
    }

    [Fact]
    public async Task SendAsync_WithConversationHistory_FlattensIntoSinglePromptText()
    {
        var handler = new StubHttpMessageHandler(_ =>
            JsonResponse(HttpStatusCode.OK, """{"candidates":[{"content":{"parts":[{"text":"ok"}]}}]}"""));
        var client = CreateClient(handler);
        var history = new List<AiChatMessage> { new("user", "earlier question") };

        await client.SendAsync("final prompt", conversationHistory: history, ct: TestContext.Current.CancellationToken);

        var body = JsonDocument.Parse(handler.LastRequest!.Body!).RootElement;
        var text = body.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString();
        text.ShouldNotBeNull();
        text.ShouldContain("earlier question");
        text.ShouldContain("final prompt");
    }

    [Fact]
    public async Task SendAsync_WithResponseJsonSchema_SetsJsonResponseMimeTypeAndSchema()
    {
        var handler = new StubHttpMessageHandler(_ =>
            JsonResponse(HttpStatusCode.OK, """{"candidates":[{"content":{"parts":[{"text":"{}"}]}}]}"""));
        var client = CreateClient(handler);

        await client.SendAsync("hi", responseJsonSchema: """{"type":"object"}""", ct: TestContext.Current.CancellationToken);

        var body = JsonDocument.Parse(handler.LastRequest!.Body!).RootElement;
        var generationConfig = body.GetProperty("generationConfig");
        generationConfig.GetProperty("responseMimeType").GetString().ShouldBe("application/json");
        generationConfig.GetProperty("responseSchema").GetProperty("type").GetString().ShouldBe("object");
    }
}
