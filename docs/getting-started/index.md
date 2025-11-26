# Getting Started with Xping SDK (.NET)

Welcome to Xping SDK. This landing page gives you the essentials before you dive into the framework-specific quick start guides.

---

## What You Get
- Trustworthy test telemetry sent directly to the Xping dashboard
- Automatic batching, retries, and diagnostics tailored for CI/CD
- Framework adapters for NUnit, xUnit, and MSTest with minimal boilerplate

---

## Prerequisites
1. **Xping account & API key** – Generate a key from the Xping dashboard under **Account → Settings → API & Integration**.
2. **ProjectId choice** – Pick a meaningful identifier. Xping creates the project on the dashboard when it receives your first upload.
3. **.NET tooling** – Test projects targeting .NET Framework 4.6.1+, .NET Core 2.0+, or .NET 5+ (use .NET SDK 6.0+ for the CLI examples in this guide).
4. **Package feed access** – Ensure your environment can restore NuGet packages from `nuget.org`.

---

## Pick Your Test Framework
| Framework | Use this guide when… | Quick link |
|-----------|----------------------|------------|
| NUnit | You decorate tests with attributes like `[Test]`, `[SetUp]`, etc. | [Quick Start - NUnit](quickstart-nunit.md) |
| xUnit | You rely on `[Fact]`/`[Theory]` and constructor-based fixtures. | [Quick Start - xUnit](quickstart-xunit.md) |
| MSTest | You use `[TestClass]`, `[TestMethod]`, and class-level initialization. | [Quick Start - MSTest](quickstart-mstest.md) |

Each guide covers adapter installation, configuration patterns, and teardown specifics for that framework.

---

## Shared Setup Flow
1. **Add the framework adapter** – e.g., `Xping.Sdk.NUnit`, `Xping.Sdk.XUnit`, or `Xping.Sdk.MSTest` (the core dependency is brought in automatically).
2. **Configure the SDK** – Provide `ApiKey`, `ProjectId`, and optional batching settings via `appsettings.json`, environment variables, or code.
3. **Initialize in tests** – Follow the framework guide to wire up `XpingContext` and required fixtures.
4. **Run tests** – Execute `dotnet test` locally or in CI; uploads appear in the Xping dashboard within seconds ([open dashboard](https://app.xping.io)).

---

## Next Steps & References
- **CI/CD Integration** – Automate secrets, environment variables, and reporting for pipelines: [CI/CD Setup](ci-cd-setup.md)
- **Configuration Reference** – Deep dive into every SDK option: [Configuration Reference](../configuration/configuration-reference.md)
- **Troubleshooting** – Diagnose auth, network, or teardown issues fast: [Common Issues](../troubleshooting/common-issues.md)

Need help? Reach us via [support@xping.io](mailto:support@xping.io) or the [GitHub Discussions board](https://github.com/xping-dev/sdk-dotnet/discussions).
