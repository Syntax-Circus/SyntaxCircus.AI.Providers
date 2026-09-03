namespace SyntaxCircus.AI.Providers;

/// <summary>One model a provider's catalog listing returned.</summary>
public sealed record AiModelInfo(string Id, string? DisplayName = null);

/// <summary>
/// The outcome of a model-catalog listing request. Mirrors <see cref="AiCompletionResult"/>'s
/// non-exception-based error handling: an unsupported/unreachable provider comes back here with
/// <see cref="Error"/> set rather than as a thrown exception.
/// </summary>
public sealed record AiModelsResult(IReadOnlyList<AiModelInfo> Models, string? Error = null)
{
    public bool Success => Error is null;
}
