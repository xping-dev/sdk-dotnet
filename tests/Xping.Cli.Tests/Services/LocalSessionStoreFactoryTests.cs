/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.Logging.Abstractions;
using Xping.Cli.Services;
using Xping.Sdk.Core.Services.Environment;
using Xping.Sdk.Core.Services.LocalStore;
using Xping.Sdk.Core.Services.LocalStore.Internals;

namespace Xping.Cli.Tests.Services;

public sealed class LocalSessionStoreFactoryTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "xping-store-factory", Guid.NewGuid().ToString("N"));

    public LocalSessionStoreFactoryTests()
    {
        Directory.CreateDirectory(Path.Combine(_temp, "repo", ".git"));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_temp, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void TheStoreIsTheOneXpingLocalStoreNames()
    {
        string fixture = Path.Combine(_temp, "fixture");

        ILocalSessionStore store = Factory(new() { ["XPING_LOCAL_STORE"] = fixture })
            .Create(startDirectory: Path.Combine(_temp, "repo"));

        Assert.Equal(fixture, store.StorePath);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void WithoutXpingLocalStoreTheStoreIsFoundFromTheStartDirectory(string? value)
    {
        Dictionary<string, string?> variables = new() { ["XPING_LOCAL_STORE"] = value };

        ILocalSessionStore store = Factory(variables).Create(startDirectory: Path.Combine(_temp, "repo"));

        Assert.Equal(Path.Combine(_temp, "repo", ".xping"), store.StorePath);
    }

    // The SDK's store starts from the test assembly's directory, the CLI's from where `xping` runs.
    // Outside a repository they used to resolve different fallback folders.
    [Fact]
    public void WithoutARepositoryTheCliFindsTheStoreTheSdkWrites()
    {
        string project = Path.Combine(_temp, "loose", "Shop.Tests");
        string output = Path.Combine(project, "bin", "Debug", "net10.0");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(project, "Shop.Tests.csproj"), "<Project />");

        var sdk = new JsonSessionStore(new LocalStoreOptions(), NullLogger.Instance, startDirectory: output);
        ILocalSessionStore cli = Factory(new()).Create(startDirectory: project);

        Assert.NotNull(sdk.StorePath);
        Assert.Equal(sdk.StorePath, cli.StorePath);
    }

    private static LocalSessionStoreFactory Factory(Dictionary<string, string?> variables) =>
        new(NullLoggerFactory.Instance, new DictionaryEnvironment(variables));

    private sealed class DictionaryEnvironment(Dictionary<string, string?> variables) : IEnvironmentVariableProvider
    {
        public string? GetVariable(string name) => variables.GetValueOrDefault(name);
    }
}
