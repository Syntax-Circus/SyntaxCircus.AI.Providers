# Release Notes: Anthropic Schema Support

## New Features

### Anthropic Client Now Supports First-Class Structured Output

The `AnthropicClient` now supports schema-constrained output via the `responseJsonSchema` parameter, bringing it to feature parity with `GeminiClient` for structured classification and extraction tasks.

#### What's New

- **Schema Parameter**: Pass a JSON Schema string to `AnthropicClient.SendAsync()` to constrain Claude's output to match your schema.
- **Client-Side Validation**: Schemas are validated client-side by default, catching malformed schemas before they reach the API.
- **Validation Bypass**: Optional `skipSchemaValidation` parameter allows advanced users to bypass client-side validation if needed.
- **Error Handling**: All schema-related errors (invalid schema, response parse failures, type mismatches) return stable `AiCompletionResult` errors without exceptions.

#### Usage Example

```csharp
var schema = """
{
  "type": "object",
  "properties": {
    "name": { "type": "string" },
    "email": { "type": "string" },
    "sentiment": { "type": "string", "enum": ["positive", "negative", "neutral"] }
  },
  "required": ["name", "email", "sentiment"]
}
""";

var result = await anthropicClient.SendAsync(
    prompt: "Extract info from: John Smith (john@example.com) loves our service!",
    responseJsonSchema: schema);

if (result.Success)
{
    var data = JsonSerializer.Deserialize<dynamic>(result.Content);
    // data is guaranteed to match the schema
}
```

## Backward Compatibility

✅ **Fully backward compatible** — existing code without schema parameters continues to work unchanged. All new parameters are optional.

## What Changed

- `AnthropicClient.SendAsync()` now accepts optional parameters:
  - `responseJsonSchema: string?` — JSON Schema string for structured output
  - `skipSchemaValidation: bool` — defaults to `false` to enable client-side validation

- New public class `SchemaValidator` provides JSON schema validation utilities
- New public record `SchemaValidationResult` represents schema validation outcomes

## Testing

- Added 10+ unit tests for schema validation
- Added 15+ unit tests for Anthropic schema support
- All tests verify schema validation, request format, response validation, and error handling
- Tested error cases: invalid schema, malformed responses, type mismatches, timeout/rate-limit stability

## API Changes Summary

- **Breaking Changes**: None
- **New Public Types**: `SchemaValidator`, `SchemaValidationResult`
- **Modified Methods**: `AnthropicClient.SendAsync()` (new optional parameters)
- **Deprecated Methods**: None

