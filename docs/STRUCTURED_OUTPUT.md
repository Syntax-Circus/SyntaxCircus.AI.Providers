# Structured Output Guide

Comprehensive guide to using JSON schemas for structured output with SyntaxCircus.AI.Providers.

## Table of Contents
- [When to Use Structured Output](#when-to-use-structured-output)
- [JSON Schema Basics](#json-schema-basics)
- [Anthropic Schema Support](#anthropic-schema-support)
- [Gemini Schema Support](#gemini-schema-support)
- [Validation & Error Handling](#validation--error-handling)
- [Common Patterns](#common-patterns)
- [Troubleshooting](#troubleshooting)

---

## When to Use Structured Output

Use structured output (schema-constrained responses) when you need:

✅ **Guaranteed JSON Response**: Model output is always valid JSON conforming to your schema  
✅ **Consistent Data Shape**: Predictable structure for parsing and processing  
✅ **Programmatic Extraction**: Extract specific fields (sentiment, entities, etc.)  
✅ **Type Safety**: Deserialize directly to strongly-typed C# objects  
✅ **API Integration**: Output that's ready to pass to other systems  

**Example Use Cases**:
- Sentiment analysis (positive/negative/neutral classification)
- Entity extraction (names, dates, amounts from text)
- Data classification (categorizing support tickets, emails)
- Information extraction (pulling structured data from unstructured text)
- Form filling (extracting values to populate forms)

---

## JSON Schema Basics

A simple JSON Schema:

```json
{
  "type": "object",
  "properties": {
    "name": { "type": "string" },
    "age": { "type": "integer" },
    "email": { "type": "string" }
  },
  "required": ["name", "email"]
}
```

### Common Schema Properties

| Property | Description | Example |
|----------|-------------|---------|
| `type` | JSON data type | `"object"`, `"string"`, `"integer"`, `"array"`, `"boolean"` |
| `properties` | Object fields | `{ "field": { "type": "string" } }` |
| `required` | Mandatory fields | `["name", "email"]` |
| `enum` | Allowed values | `["active", "inactive", "pending"]` |
| `minimum` / `maximum` | Number bounds | `"minimum": 0, "maximum": 100` |
| `items` | Array element type | `"items": { "type": "string" }` |
| `description` | Field documentation | `"description": "User's email address"` |

### Schema Examples

**Simple Classification**:
```json
{
  "type": "object",
  "properties": {
    "category": { "type": "string", "enum": ["bug", "feature", "documentation"] },
    "priority": { "type": "string", "enum": ["low", "medium", "high"] }
  },
  "required": ["category", "priority"]
}
```

**Extraction with Arrays**:
```json
{
  "type": "object",
  "properties": {
    "people": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "name": { "type": "string" },
          "age": { "type": "integer" }
        },
        "required": ["name"]
      }
    }
  },
  "required": ["people"]
}
```

**Nested Objects**:
```json
{
  "type": "object",
  "properties": {
    "user": {
      "type": "object",
      "properties": {
        "name": { "type": "string" },
        "contact": {
          "type": "object",
          "properties": {
            "email": { "type": "string" },
            "phone": { "type": "string" }
          }
        }
      }
    }
  }
}
```

---

## Anthropic Schema Support

Send schema-constrained requests to Claude using the `responseJsonSchema` parameter.

### Basic Usage

```csharp
var schema = """
{
  "type": "object",
  "properties": {
    "sentiment": { "type": "string", "enum": ["positive", "negative", "neutral"] },
    "confidence": { "type": "number", "minimum": 0, "maximum": 1 }
  },
  "required": ["sentiment", "confidence"]
}
""";

var result = await anthropicClient.SendAsync(
    prompt: "Analyze this review: 'I love this product!'",
    responseJsonSchema: schema);

if (result.Success)
{
    using var json = JsonDocument.Parse(result.Content);
    var sentiment = json.RootElement.GetProperty("sentiment").GetString();
    Console.WriteLine($"Sentiment: {sentiment}");
}
```

### Advanced: Complex Extraction

```csharp
var extractionSchema = """
{
  "type": "object",
  "properties": {
    "extracted_people": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "name": { "type": "string" },
          "title": { "type": "string" },
          "organization": { "type": "string" }
        },
        "required": ["name"]
      }
    },
    "locations_mentioned": { "type": "array", "items": { "type": "string" } },
    "dates": { "type": "array", "items": { "type": "string" } }
  },
  "required": ["extracted_people"]
}
""";

var result = await anthropicClient.SendAsync(
    prompt: """
    From this text extract all people, locations, and dates:
    John Smith from Microsoft met Jane Doe at the conference in Seattle on March 15, 2024.
    """,
    responseJsonSchema: extractionSchema);

if (result.Success)
{
    using var json = JsonDocument.Parse(result.Content);
    var people = json.RootElement.GetProperty("extracted_people");
    foreach (var person in people.EnumerateArray())
    {
        Console.WriteLine(person.GetProperty("name").GetString());
    }
}
```

### Anthropic-Specific Notes

- Schema is validated client-side by default (can be skipped with `skipSchemaValidation: true`)
- Response is validated against schema (type checking)
- Both Anthropic and Gemini support identical schema format for consistency

---

## Gemini Schema Support

Send schema-constrained requests to Gemini using the `responseJsonSchema` parameter.

### Basic Usage

```csharp
var schema = """
{
  "type": "object",
  "properties": {
    "topic": { "type": "string" },
    "key_points": { "type": "array", "items": { "type": "string" } }
  },
  "required": ["topic", "key_points"]
}
""";

var result = await geminiClient.SendAsync(
    prompt: "What are the key points of machine learning?",
    responseJsonSchema: schema);

if (result.Success)
{
    using var json = JsonDocument.Parse(result.Content);
    var keyPoints = json.RootElement.GetProperty("key_points");
    foreach (var point in keyPoints.EnumerateArray())
    {
        Console.WriteLine($"- {point.GetString()}");
    }
}
```

### Gemini-Specific Notes

- Schema is not validated client-side (relies on Gemini's validation)
- Response is automatically returned as JSON
- Supports the same schema format as Anthropic for portability

---

## Validation & Error Handling

### Client-Side Schema Validation

Anthropic validates schemas client-side before sending to the API:

```csharp
// Valid schema - passes validation
var validSchema = """{"type":"object","properties":{"name":{"type":"string"}}}""";

// Invalid schema - fails validation
var invalidSchema = """{"properties":{"name":{"type":"string"}}}"""; // Missing "type"

var result = await anthropicClient.SendAsync(
    prompt: "Extract name",
    responseJsonSchema: invalidSchema);

if (!result.Success && result.Error.Contains("Invalid schema"))
{
    Console.WriteLine("Schema validation failed before sending to API");
}
```

### Response Validation

Both clients validate that the response matches the schema:

```csharp
var result = await anthropicClient.SendAsync(
    prompt: prompt,
    responseJsonSchema: schema);

if (!result.Success)
{
    if (result.Error.Contains("does not conform to schema"))
    {
        Console.WriteLine("Response doesn't match schema type");
    }
    else if (result.Error.Contains("not valid JSON"))
    {
        Console.WriteLine("Response is not valid JSON");
    }
}
```

### Handling Schema Errors

```csharp
public async Task<T> ExtractWithSchema<T>(string text, string schema) where T : class
{
    var result = await anthropicClient.SendAsync(
        prompt: text,
        responseJsonSchema: schema);

    return result.Success switch
    {
        true => JsonSerializer.Deserialize<T>(result.Content)!,
        false when result.Error.Contains("Invalid schema") =>
            throw new ArgumentException($"Schema validation failed: {result.Error}"),
        false when result.Error.Contains("does not conform") =>
            throw new InvalidOperationException($"Response schema mismatch: {result.Error}"),
        false => throw new InvalidOperationException($"Request failed: {result.Error}")
    };
}
```

---

## Common Patterns

### Sentiment Analysis

```csharp
var sentimentSchema = """
{
  "type": "object",
  "properties": {
    "sentiment": { "type": "string", "enum": ["positive", "negative", "neutral", "mixed"] },
    "score": { "type": "number", "minimum": -1, "maximum": 1 },
    "reasoning": { "type": "string" }
  },
  "required": ["sentiment", "score"]
}
""";

var result = await anthropicClient.SendAsync(
    prompt: $"Analyze sentiment: {text}",
    responseJsonSchema: sentimentSchema);
```

### Classification

```csharp
var classificationSchema = """
{
  "type": "object",
  "properties": {
    "category": { "type": "string", "enum": ["urgent", "important", "normal", "low"] },
    "reason": { "type": "string" }
  },
  "required": ["category", "reason"]
}
""";
```

### Entity Extraction

```csharp
var entitySchema = """
{
  "type": "object",
  "properties": {
    "entities": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "text": { "type": "string" },
          "type": { "type": "string", "enum": ["PERSON", "ORGANIZATION", "LOCATION", "DATE"] },
          "confidence": { "type": "number" }
        },
        "required": ["text", "type"]
      }
    }
  },
  "required": ["entities"]
}
""";
```

---

## Troubleshooting

### "Invalid schema: Schema must define a \"type\" property"

**Problem**: Schema is missing the required `"type"` field.

```json
// ❌ Wrong - missing "type"
{ "properties": { "name": { "type": "string" } } }

// ✅ Correct
{ "type": "object", "properties": { "name": { "type": "string" } } }
```

### "Response does not conform to schema: type 'string' does not match schema type 'object'"

**Problem**: Model returned a string, but schema expects an object.

```csharp
// ❌ Wrong prompt - might return a string
var result = await client.SendAsync(
    prompt: "Extract data",
    responseJsonSchema: schema);

// ✅ Better prompt - explicitly asks for JSON object
var result = await client.SendAsync(
    prompt: "Extract data and return as a JSON object with fields: ...",
    responseJsonSchema: schema);
```

### "Response is not valid JSON"

**Problem**: Model response is not valid JSON (e.g., contains trailing text).

**Solution**: Use a clearer prompt and system message:

```csharp
var result = await client.SendAsync(
    prompt: "Extract data",
    systemPrompt: "You must respond with ONLY valid JSON matching the schema. Do not include any other text.",
    responseJsonSchema: schema);
```

### Schema Validation is Too Strict

**Problem**: Client-side schema validation is rejecting valid schemas.

**Solution** (Anthropic only): Skip client-side validation:

```csharp
var result = await anthropicClient.SendAsync(
    prompt: prompt,
    responseJsonSchema: schema,
    skipSchemaValidation: true);  // Bypasses validation, relies on API
```

---

See [EXAMPLES.md](EXAMPLES.md) for more code samples, or [PERFORMANCE.md](PERFORMANCE.md) for optimization tips.
