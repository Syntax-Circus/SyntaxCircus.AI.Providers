namespace SyntaxCircus.AI.Providers;

/// <summary>
/// The outcome of a completion request. Expected API-level failures (invalid key, rate limit,
/// 5xx) come back here rather than as a thrown exception, so callers can branch on
/// <see cref="IsRateLimited"/> without exception-driven control flow.
/// </summary>
public sealed record AiCompletionResult(
    string Content,
    int? TokensUsed = null,
    string? Error = null,
    bool IsRateLimited = false,
    DateTimeOffset? RetryAfter = null)
{
    public bool Success => Error is null;
}
