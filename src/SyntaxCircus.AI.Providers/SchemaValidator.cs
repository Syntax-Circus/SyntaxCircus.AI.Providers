using System.Text.Json;

namespace SyntaxCircus.AI.Providers;

/// <summary>
/// Validates JSON schemas for use with structured output APIs. Provides client-side validation
/// with optional bypass for advanced use cases.
/// </summary>
public static class SchemaValidator
{
    /// <summary>
    /// Validates that a JSON schema string is well-formed JSON and optionally validates it
    /// conforms to JSON Schema specification.
    /// </summary>
    /// <param name="schema">Raw JSON schema string to validate.</param>
    /// <param name="validateStructure">
    /// If true, validates the schema conforms to JSON Schema structure (requires "type", etc.).
    /// If false, only validates it is valid JSON.
    /// </param>
    /// <returns>
    /// A <see cref="SchemaValidationResult"/> with validation outcome and error details if invalid.
    /// </returns>
    public static SchemaValidationResult Validate(string schema, bool validateStructure = true)
    {
        if (string.IsNullOrWhiteSpace(schema))
        {
            return new SchemaValidationResult(IsValid: false, Error: "Schema cannot be null or empty.");
        }

        try
        {
            using var document = JsonDocument.Parse(schema);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return new SchemaValidationResult(IsValid: false, Error: "Schema must be a JSON object.");
            }

            if (validateStructure)
            {
                var structureValidation = ValidateJsonSchemaStructure(root);
                if (!structureValidation.IsValid)
                {
                    return structureValidation;
                }
            }

            return new SchemaValidationResult(IsValid: true);
        }
        catch (JsonException ex)
        {
            return new SchemaValidationResult(IsValid: false, Error: $"Invalid JSON: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates that a parsed JSON schema conforms to JSON Schema specification.
    /// Checks for required properties like "type".
    /// </summary>
    private static SchemaValidationResult ValidateJsonSchemaStructure(JsonElement schema)
    {
        if (!schema.TryGetProperty("type", out _))
        {
            return new SchemaValidationResult(
                IsValid: false,
                Error: "Schema must define a \"type\" property.");
        }

        return new SchemaValidationResult(IsValid: true);
    }
}

/// <summary>
/// Result of schema validation.
/// </summary>
public sealed record SchemaValidationResult(bool IsValid, string? Error = null);
