# GitHub Copilot Instructions for Xping SDK

## Project Overview

Xping SDK provides observability and reliability tracking for .NET test frameworks (NUnit, xUnit, MSTest). It enables developers to identify flaky tests, monitor test health, and improve test suite confidence with automatic flaky test detection and test reliability insights.

**Core Features:**
- Automatic flaky test detection through statistical analysis
- Test execution tracking with metadata collection
- Multi-framework support (NUnit, xUnit, MSTest)
- Resilient upload with retry policies and circuit breakers
- CI/CD environment detection (GitHub Actions, Azure DevOps, Jenkins, GitLab, etc.)

## Tech Stack

- **Language:** C# 12
- **Framework:** .NET 8.0
- **Testing:** NUnit (primary), xUnit, MSTest
- **Networking:** HttpClient with Polly retry policies
- **Serialization:** System.Text.Json
- **Performance Testing:** BenchmarkDotNet

## Project Structure

```
src/
├── Xping.Sdk.Core/         # Core SDK library (domain models, networking, retry logic)
├── Xping.Sdk.NUnit/        # NUnit test framework adapter
├── Xping.Sdk.XUnit/        # xUnit test framework adapter
└── Xping.Sdk.MSTest/       # MSTest test framework adapter

tests/
├── Xping.Sdk.*.Tests/      # Unit tests for each project
├── Xping.Sdk.Integration.Tests/  # Integration tests
└── Xping.Sdk.Benchmarks/   # Performance benchmarks

samples/                     # Sample applications demonstrating SDK usage
docs/                        # DocFX documentation
```

## Coding Standards

### C# Conventions (from .editorconfig)

- **Indentation:** 4 spaces
- **Line length:** 120 characters (guideline)
- **Nullable reference types:** Enabled
- **Implicit usings:** Enabled
- **Treat warnings as errors:** Yes

### File Header (Required)

Every C# file must include:

```csharp
/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */
```

### Naming Conventions

- **Classes/Interfaces:** PascalCase (interfaces prefixed with `I`)
- **Methods/Properties:** PascalCase
- **Private fields:** camelCase with underscore prefix (`_fieldName`)
- **Parameters/Local variables:** camelCase
- **Constants:** PascalCase

### Code Style Preferences

```csharp
// ✅ GOOD: Use target-typed new expressions
TestExecution execution = new()
{
    ExecutionId = Guid.NewGuid(),
    TestName = "MyTest"
};

// ✅ GOOD: Use file-scoped namespaces
namespace Xping.Sdk.Core.Models;

// ✅ GOOD: Use expression-bodied members for simple properties
public string FullName => $"{Namespace}.{TestName}";

// ✅ GOOD: Use nullable reference types
public string? ErrorMessage { get; set; }

// ✅ GOOD: Use collection expressions (C# 12)
string[] frameworks = ["NUnit", "xUnit", "MSTest"];
```

## Testing Requirements

- **Test Framework:** NUnit
- **Test Naming:** `MethodName_Scenario_ExpectedBehavior`
- **Test Structure:** AAA (Arrange-Act-Assert) pattern
- **Code Coverage:** Target >80%
- **Test Organization:** Use `[Category]` attributes

Example test:

```csharp
[TestFixture]
internal sealed class XpingApiClientTests
{
    [Test]
    [Category("Unit")]
    public async Task UploadAsync_WithValidData_ReturnsSuccess()
    {
        // Arrange
        var client = new XpingApiClient(/* ... */);
        var execution = new TestExecution { /* ... */ };

        // Act
        UploadResult result = await client.UploadAsync([execution]);

        // Assert
        Assert.That(result.IsSuccess, Is.True);
    }
}
```

## Documentation

- **XML Documentation:** Required for all public types and members
- **Format:** Use `<summary>`, `<param>`, `<returns>`, `<exception>` tags
- **Examples:** Include code examples in `<example>` tags when helpful
- **Cross-references:** Use `<see cref="..."/>` for type references

## Architecture Patterns

### Retry Policies

Use Polly for transient failure handling in HTTP clients:

```csharp
services.AddHttpClient("XpingApi")
    .AddTransientHttpErrorPolicy(builder => builder.WaitAndRetryAsync(
        sleepDurations: new[] { 
            TimeSpan.FromSeconds(1), 
            TimeSpan.FromSeconds(2), 
            TimeSpan.FromSeconds(4) 
        }))
    .AddTransientHttpErrorPolicy(builder => builder.CircuitBreakerAsync(
        handledEventsAllowedBeforeBreaking: 5,
        durationOfBreak: TimeSpan.FromSeconds(30)));
```

## Development Workflow

### When Adding Features

1. Define domain models in `src/Xping.Sdk.Core/Models/`
2. Create interface for the service contract
3. Implement service in appropriate project
4. Add comprehensive unit tests (>80% coverage)
5. Add integration tests if touching external systems
6. Update XML documentation
7. Update conceptual documentation in `docs/`

### Before Submitting PR

- [ ] Code follows .editorconfig rules
- [ ] All public APIs have XML documentation
- [ ] Unit tests added with >80% coverage
- [ ] No breaking changes (or properly documented/versioned)
- [ ] CI pipeline passes (build, test, coverage)
- [ ] No new warnings introduced

## Important Principles

1. **Minimal Changes:** Make surgical, precise changes to address specific issues
2. **Backward Compatibility:** Avoid breaking changes; use `[Obsolete]` for deprecated APIs
3. **Thread Safety:** Verify concurrent usage scenarios for shared state
4. **Performance:** Run benchmarks when changing hot paths
5. **Error Handling:** Provide actionable error messages with proper exception types
6. **Consistency:** Follow established patterns in the codebase

## Additional Resources

For detailed agent personas, interaction guidelines, and comprehensive coding conventions, see [AGENTS.md](../AGENTS.md) in the repository root.

---

**Note:** These instructions are designed for GitHub Copilot to provide context-aware suggestions. For human developers, please refer to the full documentation in the `docs/` directory and AGENTS.md.
