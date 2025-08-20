# Xping SDK - Copilot Instructions

## Project Overview

**Xping SDK** is a .NET library for automated web application and API testing. It provides tools to verify server availability, monitor content, and validate responses through a modular pipeline of test components. The name "Xping" stands for "eXternal Pings" - used to ping external servers and validate their responses.

## Core Concepts

### Key Components
- **[`TestAgent`](src/Xping.Sdk.Core/TestAgent.cs)**: Main orchestrator that runs test pipelines and collects results
- **[`TestSessionBuilder`](src/Xping.Sdk.Core/Session/TestSessionBuilder.cs)**: Builds test sessions by creating [`TestStep`](src/Xping.Sdk.Core/Session/TestStep.cs) instances
- **[`TestStep`](src/Xping.Sdk.Core/Session/TestStep.cs)**: Represents individual test operations with results and metadata
- **[`Pipeline`](src/Xping.Sdk.Core/Components/Pipeline.cs)**: Container for test components that execute in sequence
- **[`TestContext`](src/Xping.Sdk.Core/Components/TestContext.cs)**: Maintains test execution state and provides services

### Testing Pipeline
Tests execute as a pipeline of components:
1. **DNS Lookup** - Resolve domain to IP addresses
2. **IP Accessibility Check** - Ping resolved IPs
3. **HTTP/Browser Requests** - Send requests to server
4. **Response Validation** - Validate HTTP responses, HTML content, etc.

## Project Structure

```
xping-sdk/
├── src/                          # Source code
│   ├── Xping.Sdk.Core/          # Core library (interfaces, base classes)
│   ├── Xping.Sdk/               # Main SDK with implementations
│   └── Infrastructure/          # Shared infrastructure code
├── tests/                       # Unit and integration tests
├── samples/                     # Example projects
│   ├── ConsoleAppTesting/       # Console app example
│   ├── IntegrationTesting/      # ASP.NET Core integration tests
│   ├── ProductionTesting/       # Live environment monitoring
│   └── WebApp/                  # Test target web application
├── docs/                        # Documentation
├── nuspec/                      # NuGet package specifications
├── scripts/                     # Build and utility scripts
└── _build/                      # Build output directory
```

### Source Code (`src/`)

#### `Xping.Sdk.Core/`
Core library containing interfaces and base implementations:
- **Session Management**: [`ITestSessionBuilder`](src/Xping.Sdk.Core/Session/ITestSessionBuilder.cs), [`TestSession`](src/Xping.Sdk.Core/Session/TestSession.cs), [`TestStep`](src/Xping.Sdk.Core/Session/TestStep.cs)
- **Components**: [`ITestComponent`](src/Xping.Sdk.Core/Components/ITestComponent.cs), [`TestComponent`](src/Xping.Sdk.Core/Components/TestComponent.cs), [`Pipeline`](src/Xping.Sdk.Core/Components/Pipeline.cs)
- **Test Context**: [`TestContext`](src/Xping.Sdk.Core/Components/TestContext.cs), [`IInstrumentation`](src/Xping.Sdk.Core/Common/IInstrumentation.cs)
- **Test Agent**: [`TestAgent`](src/Xping.Sdk.Core/TestAgent.cs) main orchestrator
- **Serialization**: Test session serialization for saving/loading results
- **Comparison**: Tools for comparing test sessions

#### `Xping.Sdk/`
Main SDK with concrete implementations:
- **Actions**: [`DnsLookup`](src/Xping.Sdk/Actions/DnsLookup.cs), [`HttpClientRequestSender`](src/Xping.Sdk/Actions/HttpClientRequestSender.cs), [`BrowserRequestSender`](src/Xping.Sdk/Actions/BrowserRequestSender.cs)
- **Validations**: HTTP response, HTML content, browser page validations
- **Test Fixtures**: [`XpingTestFixture`](src/Xping.Sdk/XpingTestFixture.cs), [`XpingIntegrationTestFixture`](src/Xping.Sdk/XpingIntegrationTestFixture.cs)
- **Extensions**: [`TestAgentExtensions`](src/Xping.Sdk/Extensions/TestAgentExtensions.cs) for fluent API

### Tests (`tests/`)
- Unit tests for all components
- Integration tests
- Test utilities and fixtures
- Uses NUnit testing framework

### Samples (`samples/`)
Real-world examples showing different usage patterns:
- **[`ConsoleAppTesting`](samples/ConsoleAppTesting/)**: Standalone console application
- **[`IntegrationTesting`](samples/IntegrationTesting/)**: ASP.NET Core integration tests
- **[`ProductionTesting`](samples/ProductionTesting/)**: Production monitoring
- **[`WebApp`](samples/WebApp/)**: Target application for testing

## Building the Project

### Prerequisites
- .NET 8.0 SDK or higher
- PowerShell (for browser installations)

### Build Commands
```bash
# Restore dependencies
dotnet restore xping-sdk.sln

# Build solution
dotnet build xping-sdk.sln

# Run tests
dotnet test xping-sdk.sln

# Build NuGet packages
python scripts/build_nuget.py <version>
```

### Installing Headless Browsers (if using browser components)
```bash
pwsh samples/ConsoleAppTesting/bin/Debug/net8.0/playwright.ps1 install
```

## Testing the Project

### Running Tests
```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test tests/Xping.Sdk.Core.UnitTests/

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Running Samples
```bash
# Console application
cd samples/ConsoleAppTesting
dotnet run -- --url https://example.com

# Integration tests
cd samples/IntegrationTesting
dotnet test

# Production monitoring
cd samples/ProductionTesting
dotnet run
```

## Key Patterns and Conventions

### Fluent API Pattern
```csharp
testAgent
    .UseDnsLookup()
    .UseHttpClient()
    .UseHttpValidation(response => {
        Expect(response).ToHaveSuccessStatusCode();
    });
```

### Test Component Lifecycle
1. Components inherit from [`TestComponent`](src/Xping.Sdk.Core/Components/TestComponent.cs)
2. Execute via [`ExecuteAsync`](src/Xping.Sdk.Core/Components/TestComponent.cs) method
3. Report progress through [`TestContext.SessionBuilder`](src/Xping.Sdk.Core/Components/TestContext.cs)
4. Build [`TestStep`](src/Xping.Sdk.Core/Session/TestStep.cs) results

### Dependency Injection
- Uses Microsoft.Extensions.DependencyInjection
- Register services via [`AddTestAgent()`](src/Xping.Sdk.Core/DependencyInjection/DependencyInjectionExtension.cs)
- Test fixtures provide pre-configured DI containers

### Test Session Building
- [`TestSessionBuilder`](./src/Xping.Sdk.Core/Session/TestSessionBuilder.cs) creates [`TestStep`](src/Xping.Sdk.Core/Session/TestStep.cs) instances
- Each component execution creates one test step
- Steps track timing, results, and metadata via property bags

## Common Tasks

### Adding New Test Components
1. Inherit from [`TestComponent`](src/Xping.Sdk.Core/Components/TestComponent.cs)
2. Implement [`ExecuteAsync`](src/Xping.Sdk.Core/Components/TestComponent.cs) method
3. Add extension method to [`TestAgentExtensions`](src/Xping.Sdk/Extensions/TestAgentExtensions.cs)
4. Write unit tests

### Creating Test Fixtures
- Inherit from [`XpingTestFixture`](src/Xping.Sdk/XpingTestFixture.cs) for unit tests
- Use [`XpingIntegrationTestFixture<T>`](src/Xping.Sdk/XpingIntegrationTestFixture.cs) for integration tests
- Override [`ConfigureHost`](src/Xping.Sdk/XpingTestFixture.cs) to customize DI

### Working with Test Results
- [`TestSession`](src/Xping.Sdk.Core/Session/TestSession.cs) contains collection of [`TestStep`](src/Xping.Sdk.Core/Session/TestStep.cs) results
- Use [`TestSession.IsValid`](src/Xping.Sdk.Core/Session/TestSession.cs) to check overall success
- Access individual step results via [`TestSession.Steps`](src/Xping.Sdk.Core/Session/TestSession.cs)

## Important Notes

- Pipeline execution order matters - components may depend on previous results
- Use [`IInstrumentation`](src/Xping.Sdk.Core/Common/IInstrumentation.cs) for timing measurements
- Test steps are built incrementally using property bags for metadata
- Browser components require Playwright installation
- Integration tests run web applications in-memory (no browser support)
