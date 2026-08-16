namespace SyntaxCircus.AI.Providers;

/// <summary>Parses a <c>Retry-After</c> response header into an absolute point in time.</summary>
public static class RetryAfterParser
{
    public static DateTimeOffset? Parse(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (response.Headers.RetryAfter is { } retryAfter)
        {
            if (retryAfter.Delta.HasValue)
            {
                return DateTimeOffset.UtcNow + retryAfter.Delta.Value;
            }

            if (retryAfter.Date.HasValue)
            {
                return retryAfter.Date.Value;
            }
        }

        return null;
    }
}
