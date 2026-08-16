# Documentation Index

Navigation and overview of all SyntaxCircus.AI.Providers documentation.

## Quick Start by Role

### I'm a Developer - Where do I start?

1. **[Getting Started](GETTING_STARTED.md)** (5 min)
   - Installation, basic setup, first request
   - Key types: `AiCompletionResult`, `AiChatMessage`

2. **[Examples](EXAMPLES.md)** (15 min)
   - Real-world code samples
   - Text completion, chat, sentiment analysis, extraction, error handling

3. **[Structured Output](STRUCTURED_OUTPUT.md)** (10 min) — *if using schemas*
   - How to use JSON Schema for structured responses
   - Validation, common patterns, troubleshooting

4. **[Integration Guide](INTEGRATION_GUIDE.md)** (10 min) — *if adding to existing project*
   - DI setup, configuration, testing
   - Console apps, web apps, Azure Functions

5. **[API Reference](API_REFERENCE.md)** (reference)
   - Complete method signatures, parameters, error codes
   - All provider options

6. **[Troubleshooting](TROUBLESHOOTING.md)** (as needed)
   - Common errors and solutions
   - Debugging tips

---

### I'm an AI Agent - What should I understand?

1. **[Architecture](ARCHITECTURE.md)** (first)
   - System design and component overview
   - Data flow, error handling philosophy
   - Why the design is the way it is

2. **[API Reference](API_REFERENCE.md)** (then)
   - Complete type definitions
   - All public methods and parameters
   - Error codes and response structure

3. **[Design Patterns](DESIGN_PATTERNS.md)** (next)
   - Common usage patterns
   - Provider selection, error handling, conversation management
   - Schema design patterns

4. **[Examples](EXAMPLES.md)** (for context)
   - Real-world usage patterns
   - Copy-paste ready code

5. **[Contributing](CONTRIBUTING.md)** (if generating code)
   - Code style and conventions
   - How to add new providers
   - Testing and documentation requirements

---

### I'm Maintaining This Project - Full Reading Order

1. **[Architecture](ARCHITECTURE.md)** — understand the design
2. **[Getting Started](GETTING_STARTED.md)** — verify user experience
3. **[API Reference](API_REFERENCE.md)** — complete coverage
4. **[Examples](EXAMPLES.md)** — real-world validation
5. **[Design Patterns](DESIGN_PATTERNS.md)** — usage patterns
6. **[Structured Output](STRUCTURED_OUTPUT.md)** — schema feature
7. **[Integration Guide](INTEGRATION_GUIDE.md)** — user integration paths
8. **[Performance](PERFORMANCE.md)** — optimization and rate limits
9. **[Troubleshooting](TROUBLESHOOTING.md)** — common issues
10. **[Contributing](CONTRIBUTING.md)** — contributor guidelines

---

## Complete Documentation Map

### Foundations
- **[Getting Started](GETTING_STARTED.md)** — Installation, setup, first requests
- **[API Reference](API_REFERENCE.md)** — Complete API documentation

### Usage & Learning
- **[Examples](EXAMPLES.md)** — Real-world code samples
- **[Structured Output](STRUCTURED_OUTPUT.md)** — Schema/JSON output guide

### Architecture & Design
- **[Architecture](ARCHITECTURE.md)** — System design, data flow, error handling
- **[Design Patterns](DESIGN_PATTERNS.md)** — Common patterns and best practices

### Integration & Extension
- **[Integration Guide](INTEGRATION_GUIDE.md)** — Adding to projects, DI setup
- **[Contributing](CONTRIBUTING.md)** — Contributing guidelines

### Operations & Optimization
- **[Performance](PERFORMANCE.md)** — Rate limiting, optimization, cost reduction
- **[Troubleshooting](TROUBLESHOOTING.md)** — Common issues and solutions

---

## Features Overview

### ✅ Multi-Provider Support
- **Anthropic Claude** (claude-opus-5, claude-sonnet-4, etc.)
- **Google Gemini** (gemini-2.5-flash, gemini-2-pro, etc.)

See: [API Reference - Providers](API_REFERENCE.md#providers)

### ✅ Structured Output (Schema Support)
- JSON Schema validation for Anthropic
- Schema support for Gemini
- Type-safe structured responses

See: [Structured Output Guide](STRUCTURED_OUTPUT.md)

### ✅ Conversation Management
- Multi-turn conversations
- Chat history handling
- System prompts

See: [Examples - Multi-turn Chat](EXAMPLES.md#multi-turn-conversation)

### ✅ Error Handling
- Non-exception-based approach
- Rate limit detection
- Timeout handling
- Retry strategies

See: [Architecture - Error Handling](ARCHITECTURE.md#error-handling)

### ✅ Rate Limiting & Resilience
- Built-in rate limit detection
- Exponential backoff patterns
- Circuit breaker pattern

See: [Performance - Rate Limiting](PERFORMANCE.md#rate-limiting)

---

## Document Summaries

### Getting Started (5-10 min read)
**Purpose**: Quick path for new users  
**Covers**: Installation, config, DI setup, first requests, key types  
**Best for**: First-time users  
→ [Read](GETTING_STARTED.md)

### API Reference (reference)
**Purpose**: Complete API documentation  
**Covers**: Method signatures, parameters, return types, error codes  
**Best for**: Looking up specific methods or parameters  
→ [Read](API_REFERENCE.md)

### Examples (15-20 min read)
**Purpose**: Real-world usage patterns  
**Covers**: Text completion, chat, sentiment analysis, extraction, error handling, batching  
**Best for**: Learning by example, copy-paste code  
→ [Read](EXAMPLES.md)

### Structured Output (10-15 min read)
**Purpose**: Complete schema/JSON output guide  
**Covers**: When to use, JSON Schema basics, Anthropic/Gemini examples, validation, troubleshooting  
**Best for**: Using schemas for structured responses  
→ [Read](STRUCTURED_OUTPUT.md)

### Architecture (20-30 min read)
**Purpose**: Deep dive into system design  
**Covers**: Component overview, data flow, error handling philosophy, design decisions, rate limiting  
**Best for**: Contributors, AI agents, understanding design tradeoffs  
→ [Read](ARCHITECTURE.md)

### Design Patterns (15-20 min read)
**Purpose**: Common patterns and best practices  
**Covers**: Provider selection, conversation management, error handling, schema patterns, DI setup, testing  
**Best for**: Building applications, understanding patterns  
→ [Read](DESIGN_PATTERNS.md)

### Integration Guide (10-15 min read)
**Purpose**: How to integrate into projects  
**Covers**: Installation, configuration, DI setup, console/web/function apps, testing  
**Best for**: Adding to existing projects, setup questions  
→ [Read](INTEGRATION_GUIDE.md)

### Performance (15-20 min read)
**Purpose**: Optimization and rate limiting  
**Covers**: Rate limits, token optimization, retry strategies, timeouts, connection pooling, cost optimization  
**Best for**: Production deployments, cost optimization  
→ [Read](PERFORMANCE.md)

### Troubleshooting (as needed)
**Purpose**: Solutions to common issues  
**Covers**: Auth errors, API errors, schema issues, performance issues, debugging tips  
**Best for**: Debugging problems  
→ [Read](TROUBLESHOOTING.md)

### Contributing (15-20 min read)
**Purpose**: Guidelines for contributors  
**Covers**: Code style, adding providers, testing requirements, documentation, PR process  
**Best for**: Contributing code, adding features  
→ [Read](CONTRIBUTING.md)

---

## Search by Topic

### Configuration & Setup
- [Getting Started - Configuration](GETTING_STARTED.md#configuration)
- [Integration Guide - Configuration](INTEGRATION_GUIDE.md#configuration)
- [Troubleshooting - Configuration Issues](TROUBLESHOOTING.md#authentication-issues)

### Using Schemas
- [Structured Output - Complete Guide](STRUCTURED_OUTPUT.md)
- [API Reference - Schema Parameters](API_REFERENCE.md#schema-parameters)
- [Examples - Schema Usage](EXAMPLES.md#extracting-structured-data-with-schemas)
- [Design Patterns - Schema Patterns](DESIGN_PATTERNS.md#schema-design-patterns)

### Error Handling
- [Architecture - Error Handling Philosophy](ARCHITECTURE.md#error-handling)
- [Design Patterns - Error Handling Strategies](DESIGN_PATTERNS.md#error-handling-strategies)
- [Performance - Retry Strategies](PERFORMANCE.md#retry-strategies)
- [Troubleshooting - API Errors](TROUBLESHOOTING.md#api-errors)

### Rate Limiting & Performance
- [Architecture - Rate Limiting](ARCHITECTURE.md#rate-limiting)
- [Performance - Rate Limiting](PERFORMANCE.md#rate-limiting)
- [Performance - Cost Optimization](PERFORMANCE.md#cost-optimization)
- [Troubleshooting - Performance Issues](TROUBLESHOOTING.md#performance-issues)

### Testing
- [Integration Guide - Testing](INTEGRATION_GUIDE.md#testing)
- [Design Patterns - Testing Patterns](DESIGN_PATTERNS.md#testing-patterns)
- [Contributing - Testing Requirements](CONTRIBUTING.md#testing-requirements)

### Debugging
- [Troubleshooting - Debugging Tips](TROUBLESHOOTING.md#debugging-tips)
- [Troubleshooting - Logging](TROUBLESHOOTING.md#logging)

### Multi-Turn Conversations
- [Getting Started - Multi-turn Conversations](GETTING_STARTED.md#multi-turn-conversations)
- [Examples - Multi-turn Chat](EXAMPLES.md#multi-turn-conversation)
- [Design Patterns - Conversation Management](DESIGN_PATTERNS.md#conversation-management)

---

## Quick Links

**Get API Keys**
- [Anthropic Console](https://console.anthropic.com/)
- [Google AI Studio](https://aistudio.google.com/apikey)

**Provider Documentation**
- [Anthropic API Docs](https://docs.anthropic.com/)
- [Google Gemini Docs](https://ai.google.dev/docs/)

**Related Documentation**
- [JSON Schema Specification](https://json-schema.org/)
- [.NET Dependency Injection](https://docs.microsoft.com/en-us/dotnet/core/extensions/dependency-injection)

---

## File Organization

```
docs/
├── INDEX.md                     ← You are here
├── GETTING_STARTED.md           Foundation: installation & setup
├── API_REFERENCE.md             Reference: complete API
├── EXAMPLES.md                  Learning: real-world examples
├── STRUCTURED_OUTPUT.md         Feature: schemas & JSON output
├── ARCHITECTURE.md              Design: system architecture
├── DESIGN_PATTERNS.md           Patterns: common usage patterns
├── INTEGRATION_GUIDE.md         Integration: adding to projects
├── PERFORMANCE.md               Operations: optimization & rate limits
├── TROUBLESHOOTING.md           Support: solving common issues
├── CONTRIBUTING.md              Contribution: guidelines & process
```

---

## Documentation Stats

- **11 comprehensive guides** covering installation, API, examples, architecture, integration, performance, and troubleshooting
- **100+ code examples** with real-world patterns
- **Cross-linked** for easy navigation
- **AI-friendly** markdown structure for tools and agents
- **Developer-focused** with copy-paste ready samples

---

**Last Updated**: 2025  
**Version**: 1.0+  
**For issues or suggestions**: Create an issue on GitHub
