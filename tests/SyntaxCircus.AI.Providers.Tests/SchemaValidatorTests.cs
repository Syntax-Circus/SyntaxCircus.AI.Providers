namespace SyntaxCircus.AI.Providers.Tests;

public class SchemaValidatorTests
{
    [Fact]
    public void Validate_WithValidSchema_ReturnsValid()
    {
        var schema = """{"type":"object","properties":{"name":{"type":"string"}}}""";

        var result = SchemaValidator.Validate(schema);

        result.IsValid.ShouldBeTrue();
        result.Error.ShouldBeNull();
    }

    [Fact]
    public void Validate_WithNullSchema_ReturnsInvalid()
    {
        var result = SchemaValidator.Validate(null!);

        result.IsValid.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("null or empty");
    }

    [Fact]
    public void Validate_WithEmptySchema_ReturnsInvalid()
    {
        var result = SchemaValidator.Validate(string.Empty);

        result.IsValid.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("null or empty");
    }

    [Fact]
    public void Validate_WithWhitespaceSchema_ReturnsInvalid()
    {
        var result = SchemaValidator.Validate("   ");

        result.IsValid.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("null or empty");
    }

    [Fact]
    public void Validate_WithMalformedJson_ReturnsInvalid()
    {
        var schema = """{"type":"object"invalid}""";

        var result = SchemaValidator.Validate(schema);

        result.IsValid.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("Invalid JSON");
    }

    [Fact]
    public void Validate_WithJsonArray_ReturnsInvalid()
    {
        var schema = """[{"type":"object"}]""";

        var result = SchemaValidator.Validate(schema);

        result.IsValid.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("must be a JSON object");
    }

    [Fact]
    public void Validate_WithSchemaWithoutType_ReturnsInvalid()
    {
        var schema = """{"properties":{"name":{"type":"string"}}}""";

        var result = SchemaValidator.Validate(schema, validateStructure: true);

        result.IsValid.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("must define a \"type\"");
    }

    [Fact]
    public void Validate_WithSchemaWithoutType_ButSkipValidation_ReturnsValid()
    {
        var schema = """{"properties":{"name":{"type":"string"}}}""";

        var result = SchemaValidator.Validate(schema, validateStructure: false);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithComplexValidSchema_ReturnsValid()
    {
        var schema = """
        {
          "type": "object",
          "properties": {
            "id": {"type": "integer"},
            "name": {"type": "string"},
            "email": {"type": "string"},
            "active": {"type": "boolean"}
          },
          "required": ["id", "name"]
        }
        """;

        var result = SchemaValidator.Validate(schema);

        result.IsValid.ShouldBeTrue();
        result.Error.ShouldBeNull();
    }
}
