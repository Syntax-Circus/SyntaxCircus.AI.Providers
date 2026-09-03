namespace SyntaxCircus.AI.Providers.Tests;

public class OpenAiCompatibleClientTests
{
    private const string DefaultBaseUrl = "https://openrouter.ai/api/v1/";

    private static OpenAiCompatibleClient CreateClient(
        StubHttpMessageHandler handler,
        string apiKey = "test-key",
        string model = "test-model",
        string baseUrl = DefaultBaseUrl)
    {
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new OpenAiCompatibleClientOptions { ApiKey = apiKey, Model = model, BaseUrl = baseUrl });
        return new OpenAiCompatibleClient(httpClient, options);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json) =>
        new(statusCode) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };

    [Fact]
    public async Task SendAsync_OnSuccess_ReturnsContentAndTokensUsed()
    {
        var handler = new StubHttpMessageHandler(_ =>
            JsonResponse(HttpStatusCode.OK, """{"choices":[{"message":{"content":"Hi there"}}],"usage":{"total_tokens":7}}"""));
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
        result.Error.ShouldBe("OpenAI-compatible API key is not configured.");
        handler.LastRequest.ShouldBeNull();
    }

    [Fact]
    public async Task SendAsync_WithNoBaseUrlConfigured_ReturnsErrorWithoutSendingRequest()
    {
        var handler = new StubHttpMessageHandler(_ => throw new InvalidOperationException("Should not be called."));
        var client = CreateClient(handler, baseUrl: string.Empty);

        var result = await client.SendAsync("hi", ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.Error.ShouldBe("OpenAI-compatible base URL is not configured.");
        handler.LastRequest.ShouldBeNull();
    }

    [Fact]
    public async Task SendAsync_WithApiKeyOverride_UsesOverrideInAuthorizationHeader()
    {
        var handler = new StubHttpMessageHandler(_ =>
            JsonResponse(HttpStatusCode.OK, """{"choices":[{"message":{"content":"ok"}}]}"""));
        var client = CreateClient(handler, apiKey: string.Empty);

        await client.SendAsync("hi", ct: TestContext.Current.CancellationToken, apiKeyOverride: "runtime-key");

        handler.LastRequest!.HeaderValue("Authorization").ShouldBe("Bearer runtime-key");
    }

    [Fact]
    public async Task SendAsync_WithModelOverride_UsesOverrideModelInRequestBody()
    {
        var handler = new StubHttpMessageHandler(_ =>
            JsonResponse(HttpStatusCode.OK, """{"choices":[{"message":{"content":"ok"}}]}"""));
        var client = CreateClient(handler, model: "configured-model");

        await client.SendAsync(
            "hi",
            apiKeyOverride: null,
            systemPrompt: null,
            conversationHistory: null,
            responseJsonSchema: null,
            modelOverride: "override-model",
            ct: TestContext.Current.CancellationToken);

        var body = JsonDocument.Parse(handler.LastRequest!.Body!).RootElement;
        body.GetProperty("model").GetString().ShouldBe("override-model");
    }

    [Fact]
    public async Task SendAsync_WithBaseUrlOverride_PostsToOverrideUrl()
    {
        var handler = new StubHttpMessageHandler(_ =>
            JsonResponse(HttpStatusCode.OK, """{"choices":[{"message":{"content":"ok"}}]}"""));
        var client = CreateClient(handler, baseUrl: DefaultBaseUrl);

        await client.SendAsync(
            "hi",
            apiKeyOverride: null,
            systemPrompt: null,
            conversationHistory: null,
            responseJsonSchema: null,
            modelOverride: null,
            baseUrlOverride: "https://custom.example.com/v1/",
            ct: TestContext.Current.CancellationToken);

        handler.LastRequest!.RequestUri!.ToString().ShouldBe("https://custom.example.com/v1/chat/completions");
    }

    [Fact]
    public async Task SendAsync_NormalizesBaseUrlRegardlessOfTrailingSlash()
    {
        var handler = new StubHttpMessageHandler(_ =>
            JsonResponse(HttpStatusCode.OK, """{"choices":[{"message":{"content":"ok"}}]}"""));
        var client = CreateClient(handler, baseUrl: "https://openrouter.ai/api/v1");

        await client.SendAsync("hi", ct: TestContext.Current.CancellationToken);

        handler.LastRequest!.RequestUri!.ToString().ShouldBe("https://openrouter.ai/api/v1/chat/completions");
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
    public async Task SendAsync_On401_ReturnsInvalidApiKeyError()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var client = CreateClient(handler);

        var result = await client.SendAsync("hi", ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.Error.ShouldBe("Invalid API key.");
    }

    [Fact]
    public async Task SendAsync_On403_ReturnsInvalidApiKeyError()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));
        var client = CreateClient(handler);

        var result = await client.SendAsync("hi", ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.Error.ShouldBe("Invalid API key.");
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
    public async Task SendAsync_On500_ReturnsErrorWithoutThrowing()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var client = CreateClient(handler);

        var result = await client.SendAsync("hi", ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task SendAsync_WithEmptyChoices_ReturnsEmptyResponseError()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(HttpStatusCode.OK, """{"choices":[]}"""));
        var client = CreateClient(handler);

        var result = await client.SendAsync("hi", ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.Error.ShouldBe("Empty response from provider.");
    }

    [Fact]
    public async Task SendAsync_OnMalformedJson_ReturnsErrorWithoutThrowing()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(HttpStatusCode.OK, "not valid json"));
        var client = CreateClient(handler);

        var result = await client.SendAsync("hi", ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.Error.ShouldBe("Malformed response from provider.");
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
    public async Task SendAsync_WithSystemPrompt_IncludesSystemMessage()
    {
        var handler = new StubHttpMessageHandler(_ =>
            JsonResponse(HttpStatusCode.OK, """{"choices":[{"message":{"content":"ok"}}]}"""));
        var client = CreateClient(handler);

        await client.SendAsync("hi", systemPrompt: "Be nice.", ct: TestContext.Current.CancellationToken);

        var body = JsonDocument.Parse(handler.LastRequest!.Body!).RootElement;
        var messages = body.GetProperty("messages");
        messages.GetArrayLength().ShouldBe(2);
        messages[0].GetProperty("role").GetString().ShouldBe("system");
        messages[0].GetProperty("content").GetString().ShouldBe("Be nice.");
        messages[1].GetProperty("role").GetString().ShouldBe("user");
        messages[1].GetProperty("content").GetString().ShouldBe("hi");
    }

    [Fact]
    public async Task SendAsync_WithoutSystemPrompt_OmitsSystemMessage()
    {
        var handler = new StubHttpMessageHandler(_ =>
            JsonResponse(HttpStatusCode.OK, """{"choices":[{"message":{"content":"ok"}}]}"""));
        var client = CreateClient(handler);

        await client.SendAsync("hi", ct: TestContext.Current.CancellationToken);

        var body = JsonDocument.Parse(handler.LastRequest!.Body!).RootElement;
        var messages = body.GetProperty("messages");
        messages.GetArrayLength().ShouldBe(1);
        messages[0].GetProperty("role").GetString().ShouldBe("user");
    }

    [Fact]
    public async Task SendAsync_WithConversationHistory_MapsEachTurnIntoMessagesArray()
    {
        var handler = new StubHttpMessageHandler(_ =>
            JsonResponse(HttpStatusCode.OK, """{"choices":[{"message":{"content":"ok"}}]}"""));
        var client = CreateClient(handler);
        var history = new List<AiChatMessage> { new("user", "earlier question"), new("assistant", "earlier answer") };

        await client.SendAsync("final prompt", conversationHistory: history, ct: TestContext.Current.CancellationToken);

        var body = JsonDocument.Parse(handler.LastRequest!.Body!).RootElement;
        var messages = body.GetProperty("messages");
        messages.GetArrayLength().ShouldBe(3);
        messages[0].GetProperty("content").GetString().ShouldBe("earlier question");
        messages[1].GetProperty("content").GetString().ShouldBe("earlier answer");
        messages[2].GetProperty("content").GetString().ShouldBe("final prompt");
    }

    [Fact]
    public async Task SendAsync_SendsApiKeyViaAuthorizationHeader()
    {
        var handler = new StubHttpMessageHandler(_ =>
            JsonResponse(HttpStatusCode.OK, """{"choices":[{"message":{"content":"ok"}}]}"""));
        var client = CreateClient(handler, apiKey: "secret-key");

        await client.SendAsync("hi", ct: TestContext.Current.CancellationToken);

        handler.LastRequest.ShouldNotBeNull();
        handler.LastRequest.HeaderValue("Authorization").ShouldBe("Bearer secret-key");
    }

    [Fact]
    public async Task SendAsync_SendsMaxTokensFromOptions()
    {
        var handler = new StubHttpMessageHandler(_ =>
            JsonResponse(HttpStatusCode.OK, """{"choices":[{"message":{"content":"ok"}}]}"""));
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new OpenAiCompatibleClientOptions { ApiKey = "key", Model = "m", BaseUrl = DefaultBaseUrl, MaxTokens = 123 });
        var client = new OpenAiCompatibleClient(httpClient, options);

        await client.SendAsync("hi", ct: TestContext.Current.CancellationToken);

        var body = JsonDocument.Parse(handler.LastRequest!.Body!).RootElement;
        body.GetProperty("max_tokens").GetInt32().ShouldBe(123);
    }
}
