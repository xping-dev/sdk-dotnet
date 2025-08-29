# Xping SDK - GitHub Copilot Instructions

**ALWAYS reference these instructions first before performing any actions or running commands. Only fallback to additional search, exploration, or commands when the information in these instructions is incomplete or found to be incorrect.**

## Project Overview

**Xping SDK** is a .NET library for automated web application and API testing. It provides tools to verify server availability, monitor content, and validate responses through a modular pipeline of test components. The name "Xping" stands for "eXternal Pings" - used to ping external servers and validate their responses.

## Working Effectively

### CRITICAL - Build, Test, and Validation Commands

**ALWAYS run these commands in the exact order specified with the documented timeouts:**

1. **Restore Dependencies** (30 seconds - NEVER CANCEL)
```bash
cd /path/to/repo && dotnet restore xping-sdk.sln
```
- Timeout: Set to 60+ seconds minimum
- Expected duration: ~27 seconds
- Status: WORKING ✓

2. **Build Solution** (30 seconds - NEVER CANCEL)
```bash
cd /path/to/repo && dotnet build xping-sdk.sln -c Release --no-restore
```
- Timeout: Set to 60+ seconds minimum
- Expected duration: ~26 seconds
- Status: WORKING ✓

3. **Run Unit Tests** (60 seconds - NEVER CANCEL)
```bash
cd /path/to/repo && dotnet test tests/Xping.Sdk.Core.UnitTests/ -c Release --no-build
```
- Timeout: Set to 120+ seconds minimum
- Expected duration: ~3 seconds
- Status: WORKING ✓ (428 tests pass)

4. **Browser Installation** (3+ minutes - NEVER CANCEL - KNOWN TO FAIL)
```bash
cd /path/to/repo && pwsh tests/Xping.Sdk.IntegrationTests/bin/Release/net8.0/playwright.ps1 install --with-deps
```
- Timeout: Set to 300+ seconds minimum
- Expected duration: ~2+ minutes
- Status: **FAILS due to Playwright download issues - documented limitation**

### Code Quality and Formatting

**Code formatting has been applied and should not be modified:**

1. **Check Code Formatting Status** (40 seconds - NEVER CANCEL)
```bash
cd /path/to/repo && dotnet format --verify-no-changes --verbosity diagnostic
```
- Timeout: Set to 120+ seconds minimum
- Expected duration: ~38 seconds  
- Status: **WORKING - formatting has been applied, build succeeds**

2. **Do NOT run dotnet format again**
```bash
# DO NOT RUN: dotnet format without --verify-no-changes
```
- **REASON**: Formatting has already been applied and working
- **CURRENT STATE**: Build works correctly with current formatting

### Validation Scenarios

**MANDATORY: Always test these scenarios after making any changes:**

1. **Core Unit Tests Validation**
```bash
cd /path/to/repo && dotnet test tests/Xping.Sdk.Core.UnitTests/ -c Release --no-build
```
- Must pass: 428+ tests
- Must skip: 2 tests max
- Verify: Zero failures

2. **Integration Testing Sample**
```bash
cd /path/to/repo/samples/IntegrationTesting && dotnet test --configuration Release --no-build
```
- Must pass: 4+ tests
- Expected duration: ~2 seconds
- Verify: Tests run against in-memory web application

3. **NuGet Package Build**
```bash
cd /path/to/repo && python scripts/build_nuget.py 1.0.0-test
```
- Must create: Xping.Sdk.Core.1.0.0-test.nupkg
- Must create: Xping.Sdk.1.0.0-test.nupkg
- Expected duration: ~5 seconds

## Repository Structure

```
/home/runner/work/sdk-dotnet/sdk-dotnet/
├── .github/                      # GitHub configuration
│   ├── workflows/               # CI/CD pipelines
│   │   ├── ci.yml              # Main CI pipeline
│   │   └── release.yml         # Release pipeline
│   └── copilot-instructions.md # This file
├── src/                         # Source code
│   ├── Xping.Sdk.Core/         # Core library (interfaces, base classes)
│   ├── Xping.Sdk/              # Main SDK with implementations  
│   └── Xping.Sdk.Shared/       # Shared code project
├── tests/                       # Unit and integration tests
│   ├── Xping.Sdk.Core.UnitTests/           # Core unit tests
│   ├── Xping.Sdk.Availability.UnitTests/   # Availability tests
│   ├── Xping.Sdk.Shared.UnitTests/         # Shared tests
│   └── Xping.Sdk.IntegrationTests/         # Integration tests
├── samples/                     # Example projects
│   ├── ConsoleAppTesting/       # Console app example
│   ├── IntegrationTesting/      # ASP.NET Core integration tests
│   ├── ProductionTesting/       # Live environment monitoring
│   └── WebApp/                  # Test target web application
├── docs/                        # Documentation
├── scripts/                     # Build and utility scripts
│   ├── build_nuget.py          # NuGet package builder
│   └── BUILD_NUGET_README.md   # Build script documentation
├── xping-sdk.sln               # Main solution file
├── Directory.Build.props        # MSBuild properties
└── .editorconfig               # Code style configuration
```

### Key Project Files

#### Main Projects
- **`Xping.Sdk.Core.csproj`**: Core library with interfaces and base classes
- **`Xping.Sdk.csproj`**: Main SDK implementation 
- **`Xping.Sdk.Shared.shproj`**: Shared code project

#### Test Projects
- **`Xping.Sdk.Core.UnitTests.csproj`**: 428+ unit tests for core functionality
- **`Xping.Sdk.Availability.UnitTests.csproj`**: 124+ tests for availability components
- **`Xping.Sdk.IntegrationTests.csproj`**: Browser-dependent integration tests

#### Sample Projects  
- **`ConsoleAppTesting.csproj`**: Standalone console application
- **`IntegrationTesting.csproj`**: ASP.NET Core in-memory testing
- **`ProductionTesting.csproj`**: Production monitoring examples
- **`WebApp.csproj`**: Target web application for testing

## Known Working Commands

### Prerequisites
- **.NET 8.0 SDK**: Version 8.0.119+ required
- **PowerShell**: Available at `/usr/bin/pwsh`
- **Python**: Available for NuGet build scripts

### Build and Test Workflow

**Execute in this exact order:**

1. Navigate to repository root
2. Restore dependencies (NEVER CANCEL - 60+ second timeout)
3. Build solution (NEVER CANCEL - 60+ second timeout)  
4. Run unit tests (NEVER CANCEL - 120+ second timeout)
5. Run validation scenarios

### Sample Application Testing

1. **Console Application** (Network dependent)
```bash
cd /path/to/repo/samples/ConsoleAppTesting
dotnet run --configuration Release -- --url https://httpbin.org/get
```
- Status: Network dependent - may fail in restricted environments

2. **Integration Testing** (In-memory)
```bash
cd /path/to/repo/samples/IntegrationTesting  
dotnet test --configuration Release --no-build
```
- Status: WORKING ✓ - runs web app in memory

3. **Production Testing** (Browser dependent)
```bash
cd /path/to/repo/samples/ProductionTests
dotnet test --configuration Release --no-build
```
- Status: FAILS without browser installation

4. **WebApp Sample** (Manual testing)
```bash
cd /path/to/repo/samples/WebApp
dotnet run --configuration Release --urls http://localhost:5000
```
- Status: WORKING ✓ - serves web application on http://localhost:5000
- Expected response: HTML page with title "Home page - WebApp"

## Core Development Concepts

### Key Components
- **`TestAgent`**: Main orchestrator (src/Xping.Sdk.Core/TestAgent.cs)
- **`TestSessionBuilder`**: Session builder (src/Xping.Sdk.Core/Session/TestSessionBuilder.cs)
- **`TestStep`**: Individual operations (src/Xping.Sdk.Core/Session/TestStep.cs)
- **`Pipeline`**: Component container (src/Xping.Sdk.Core/Components/Pipeline.cs)
- **`TestContext`**: Execution state (src/Xping.Sdk.Core/Components/TestContext.cs)

### Testing Pipeline
Tests execute as a pipeline of components:
1. **DNS Lookup** - Resolve domain to IP addresses
2. **IP Accessibility Check** - Ping resolved IPs  
3. **HTTP/Browser Requests** - Send requests to server
4. **Response Validation** - Validate HTTP responses, HTML content

### Fluent API Pattern
```csharp
testAgent
    .UseDnsLookup()
    .UseHttpClient()
    .UseHttpValidation(response => {
        Expect(response).ToHaveSuccessStatusCode();
    });
```

## Common Tasks

### Adding New Test Components
1. Inherit from `TestComponent` (src/Xping.Sdk.Core/Components/TestComponent.cs)
2. Implement `ExecuteAsync` method
3. Add extension method to `TestAgentExtensions` (src/Xping.Sdk/Extensions/TestAgentExtensions.cs)
4. Write unit tests

### Running Tests After Changes
**ALWAYS execute these validation steps in order:**
1. Build solution: `dotnet build xping-sdk.sln -c Release --no-restore`
2. Test core: `dotnet test tests/Xping.Sdk.Core.UnitTests/ -c Release --no-build`
3. Test integration: `cd samples/IntegrationTesting && dotnet test -c Release --no-build`
4. Validate NuGet build: `python scripts/build_nuget.py 1.0.0-test`

### Browser Components
- **Installation required**: `pwsh playwright.ps1 install` (fails with download issues)
- **Alternative**: Use HttpClient components for testing
- **Integration tests**: Run web apps in-memory (no browser UI support)

## Important Limitations

1. **Browser Installation**: Playwright browser download fails due to network/CDN issues
2. **Network Dependencies**: Some console tests require external connectivity  
3. **Code Formatting**: Repository has been formatted, do not run dotnet format again
4. **Shared Projects**: `.shproj` files cannot be processed by some tools
5. **Build State**: Build succeeds with current formatting applied

## Working Around Known Issues

1. **For browser tests**: Use integration testing samples instead of browser-dependent tests
2. **For network issues**: Test with local/in-memory scenarios when possible
3. **For formatting**: Use --verify-no-changes flag only to check status
4. **For timeouts**: Always set timeouts to 2x expected duration minimum

## Manual Validation Requirements

**ALWAYS perform end-to-end testing after making changes:**

1. **Build Pipeline Test**:
   - Run: `dotnet restore && dotnet build -c Release --no-restore`
   - Verify: Zero errors, builds successfully
   - Time: ~53 seconds total

2. **Core Functionality Test**:
   - Run: `dotnet test tests/Xping.Sdk.Core.UnitTests/ -c Release --no-build`
   - Verify: 428+ tests pass, 2 skipped max
   - Time: ~3 seconds

3. **Integration Test**:
   - Run: `cd samples/IntegrationTesting && dotnet test -c Release --no-build`
   - Verify: 4+ tests pass, web app works in-memory
   - Time: ~2 seconds

4. **Package Build Test**:
   - Run: `python scripts/build_nuget.py 1.0.0-test`
   - Verify: Two .nupkg files created in _build/nuget/
   - Time: ~5 seconds

**Remember: NEVER CANCEL long-running builds or tests. Build operations can take 30+ seconds, tests can take 60+ seconds, and formatting can take 40+ seconds. These are normal durations for this codebase.**
