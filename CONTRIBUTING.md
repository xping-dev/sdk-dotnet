# Contributing

## Build and test

```sh
dotnet build -c Release                              # warnings are errors
dotnet test tests/Xping.Sdk.Core.Tests -c Release    # one project; see ci.yml for the full list
dotnet format --verify-no-changes                    # style check
```

Branch off `main`, one issue per PR, and use conventional commits with a scope:
`fix(sdk): …`, `feat(cli): …`, `docs: …`.

## Dogfooding

The SDK records its own test runs. CI uploads them to Xping Cloud, and locally they land in the
`.xping/` store that `xping report` reads.

**What is tracked.** Every test project runs on xUnit, so the xUnit adapter does the recording,
registered in each project's `AssemblyInfo.cs`:

```csharp
[assembly: TestFramework("Xping.Sdk.XUnit.XpingTestFramework", "Xping.Sdk.XUnit")]
```

Core, NUnit adapter, MSTest adapter, Integration and CLI tests are tracked. `Xping.Sdk.XUnit.Tests`
is not: its tests shut down and recreate the static `XpingContext` the framework records into,
which would split the run into fragments.

**Project names.** There is no configuration. Each test assembly reports into a Xping Cloud
project named after it (`Xping.Sdk.Core.Tests`, `Xping.Cli.Tests`, …), which is the SDK default.

**CI.** `ci.yml` sets `XPING_ENABLED=true` and `XPING_APIKEY` from the `XPING_API_KEY` repository
secret, which puts the SDK in Cloud mode. Pull requests from forks don't get secrets, so their runs
stay local.

**Locally.** Without an API key, results go to the local store only. With `XPING_APIKEY` set in
your shell, they are uploaded too. To keep a run local anyway, set `XPING_MODE=LocalOnly`; to turn
Xping off, set `XPING_ENABLED=false`.

Local uploads land in the same projects as CI, so tag them to keep your debugging runs out of the
CI numbers:

```sh
export XPING_APIKEY=<your local key>
export XPING_ENVIRONMENT=Local
```

**Tests that create their own context.** Pass an explicit configuration with
`Mode = XpingMode.LocalOnly` and a scratch `LocalStorePath`, or a loopback `ApiEndpoint` when the
test needs the upload path. Never build one from the ambient configuration: in CI it carries the
real API key, and the test's synthetic session would be uploaded next to the real ones.
