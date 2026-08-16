namespace SyntaxCircus.AI.Providers.Tests;

public class AnthropicClientTests
{
    private static AnthropicClient CreateClient(StubHttpMessageHandler handler, string apiKey = "test-key")
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/") };
        var options = Options.Create(new AnthropicClientOptions { ApiKey = apiKey });
        return new AnthropicClient(httpClient, options);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json) =>
        new(statusCode) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };

    [Fact]
    public async Task SendAsync_OnSuccess_ReturnsContentAndTokensUsed()
    {
        var handler = new StubHttpMessageHandler(_ =>
            JsonResponse(HttpStatusCode.OK, """{"content":[{"text":"Hello there"}],"usage":{"output_tokens":42}}"""));
        var client = CreateClient(handler);

        var result = await client.SendAsync("hi", ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Content.ShouldBe("Hello there");
        result.TokensUsed.ShouldBe(42);
    }

    [Fact]
    public async Task SendAsync_WithNoApiKeyConfigured_ReturnsErrorWithoutSendingRequest()
    {
        var handler = new StubHttpMessageHandler(_ => throw new InvalidOperationException("Should not be called."));
        var client = CreateClient(handler, apiKey: string.Empty);

        var result = await client.SendAsync("hi", ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.Error.ShouldBe("Anthropic API key is not configured.");
        handler.LastRequest.ShouldBeNull();
    }

    [Fact]
    public async Task SendAsync_On401_ReturnsInvalidApiKeyError()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var client = CreateClient(handler);

        var result = await client.SendAsync("hi", ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.Error.ShouldBe("Invalid Anthropic API key.");
    }

    [Fact]
    public async Task SendAsync_On429_ReturnsRateLimitedResultWithRetryAfter()
    {
        var handler = new StubHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(15));
            return response;
        });
        var client = CreateClient(handler);

        var result = await client.SendAsync("hi", ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.IsRateLimited.ShouldBeTrue();
        result.RetryAfter.ShouldNotBeNull();
        result.RetryAfter.Value.ShouldBeInRange(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddSeconds(15));
    }

    [Fact]
    public async Task SendAsync_On5xx_ReturnsServerErrorWithStatusCode()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var client = CreateClient(handler);

        var result = await client.SendAsync("hi", ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.Error.ShouldBe("Anthropic API error (503).");
    }

    [Fact]
    public async Task SendAsync_OnMalformedJson_ReturnsError()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(HttpStatusCode.OK, "not valid json"));
        var client = CreateClient(handler);

        var result = await client.SendAsync("hi", ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.Error.ShouldBe("Malformed response from Anthropic API.");
    }

    [Fact]
    public async Task SendAsync_WithEmptyContentArray_ReturnsError()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(HttpStatusCode.OK, """{"content":[]}"""));
        var client = CreateClient(handler);

        var result = await client.SendAsync("hi", ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.Error.ShouldBe("Empty response from Anthropic API.");
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
    public async Task SendAsync_OnTimeout_ReturnsTimeoutError()
    {
        var handler = new StubHttpMessageHandler(_ => throw new TaskCanceledException());
        var client = CreateClient(handler);

        var result = await client.SendAsync("hi", ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.Error.ShouldBe("Request timed out.");
    }

    [Fact]
    public async Task SendAsync_IncludesApiKeyAndVersionHeaders()
    {
        var handler = new StubHttpMessageHandler(_ =>
            JsonResponse(HttpStatusCode.OK, """{"content":[{"text":"ok"}]}"""));
        var client = CreateClient(handler, apiKey: "secret-key");

        await client.SendAsync("hi", ct: TestContext.Current.CancellationToken);

        handler.LastRequest.ShouldNotBeNull();
        handler.LastRequest.HeaderValue("x-api-key").ShouldBe("secret-key");
        handler.LastRequest.HeaderValue("anthropic-version").ShouldBe("2023-06-01");
    }

    [Fact]
    public async Task SendAsync_WithConversationHistory_SendsHistoryThenPromptInOrder()
    {
        var handler = new StubHttpMessageHandler(_ =>
            JsonResponse(HttpStatusCode.OK, """{"content":[{"text":"ok"}]}"""));
        var client = CreateClient(handler);
        var history = new List<AiChatMessage> { new("user", "earlier question"), new("assistant", "earlier answer") };

        await client.SendAsync("final prompt", conversationHistory: history, ct: TestContext.Current.CancellationToken);

        var body = JsonDocument.Parse(handler.LastRequest!.Body!).RootElement;
        var messages = body.GetProperty("messages");
        messages.GetArrayLength().ShouldBe(3);
        messages[0].GetProperty("content").GetString().ShouldBe("earlier question");
        messages[1].GetProperty("content").GetString().ShouldBe("earlier answer");
        messages[2].GetProperty("role").GetString().ShouldBe("user");
        messages[2].GetProperty("content").GetString().ShouldBe("final prompt");
    }

    [Fact]
    public async Task SendAsync_SendsConfiguredModelAndMaxTokens()
    {
        var handler = new StubHttpMessageHandler(_ =>
            JsonResponse(HttpStatusCode.OK, """{"content":[{"text":"ok"}]}"""));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/") };
        var options = Options.Create(new AnthropicClientOptions { ApiKey = "key", Model = "claude-test-model", MaxTokens = 128 });
        var client = new AnthropicClient(httpClient, options);

        await client.SendAsync("hi", ct: TestContext.Current.CancellationToken);

        var body = JsonDocument.Parse(handler.LastRequest!.Body!).RootElement;
        body.GetProperty("model").GetString().ShouldBe("claude-test-model");
        body.GetProperty("max_tokens").GetInt32().ShouldBe(128);
    }

    [Fact]
    public async Task SendAsync_WithResponseJsonSchema_SendsStructuredOutputToolDefinition()
    {
        var handler = new StubHttpMessageHandler(_ =>
            JsonResponse(HttpStatusCode.OK, """{"content":[{"type":"tool_use","input":{"verdict":"allow","confidence":0.9}}]}"""));
        var client = CreateClient(handler);

        await client.SendAsync("hi", responseJsonSchema: """{"type":"object"}""", ct: TestContext.Current.CancellationToken);

        var body = JsonDocument.Parse(handler.LastRequest!.Body!).RootElement;
        var tools = body.GetProperty("tools");
        tools.GetArrayLength().ShouldBe(1);
        tools[0].GetProperty("name").GetString().ShouldBe("structured_output");
        tools[0].GetProperty("description").GetString().ShouldBe("Return the response as structured JSON.");
        tools[0].GetProperty("input_schema").GetProperty("type").GetString().ShouldBe("object");

        var toolChoice = body.GetProperty("tool_choice");
        toolChoice.GetProperty("type").GetString().ShouldBe("tool");
        toolChoice.GetProperty("name").GetString().ShouldBe("structured_output");
    }

    [Fact]
    public async Task SendAsync_WithResponseJsonSchema_ReturnsToolUseInputAsJson()
    {
        var handler = new StubHttpMessageHandler(_ =>
            JsonResponse(HttpStatusCode.OK, """{"content":[{"type":"tool_use","input":{"verdict":"allow","category":null,"confidence":0.75}}],"usage":{"output_tokens":11}}"""));
        var client = CreateClient(handler);

        var result = await client.SendAsync("hi", responseJsonSchema: """{"type":"object"}""", ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        using var output = JsonDocument.Parse(result.Content);
        output.RootElement.GetProperty("verdict").GetString().ShouldBe("allow");
        output.RootElement.GetProperty("confidence").GetDouble().ShouldBe(0.75);
        result.TokensUsed.ShouldBe(11);
    }
}
