namespace SyntaxCircus.AI.Providers;

/// <summary>A single turn in a conversation history passed to <see cref="AnthropicClient"/> or <see cref="GeminiClient"/>.</summary>
public sealed record AiChatMessage(string Role, string Content);
