/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.LocalStore;
using Xping.Sdk.Core.Services.LocalStore.Internals;

namespace Xping.Sdk.Core.Tests.LocalStore;

// Mutates the XPING_LOCAL_STORE environment variable, which is process-wide state.
[Collection("Sequential")]
public sealed class LocalStoreReviewFixesTests : IDisposable
{
    private readonly string _root;

    public LocalStoreReviewFixesTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "xping-review-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        System.Environment.SetEnvironmentVariable(
            LocalStorePathResolver.EnvironmentVariableName, _root);
    }

    public void Dispose()
    {
        System.Environment.SetEnvironmentVariable(
            LocalStorePathResolver.EnvironmentVariableName, null);

        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static TestSession BuildSession(
        string name = "T", IDictionary<string, string>? customProperties = null) =>
        new TestSessionBuilder()
            .WithSessionId(Guid.NewGuid())
            .WithStartedAt(DateTime.UtcNow)
            .WithEnvironmentInfo(new EnvironmentInfoBuilder()
                .AddCustomProperties(customProperties ?? new Dictionary<string, string>())
                .Build())
            .AddExecutions(
            [
                new TestExecutionBuilder()
                    .WithIdentity(new TestIdentityBuilder()
                        .WithTestFingerprint("fp")
                        .WithAssembly("MyApp.Tests")
                        .WithDisplayName(name)
                        .Build())
                    .WithTestName(name)
                    .WithOutcome(TestOutcome.Passed)
                    .Build()
            ])
            .WithSessionState(TestSessionState.Finalized)
            .Build();

    private static JsonSessionStore CreateStore() =>
        new(new LocalStoreOptions(), NullLogger.Instance);

    [Fact]
    public void AMalformedSessionDoesNotBreakDelete()
    {
        // Arrange — a scoped delete has to read each file to learn its assembly, so a corrupt file
        // must cost that file alone rather than the whole operation.
        JsonSessionStore store = CreateStore();
        store.Write(BuildSession("good"));

        string path = Path.Combine(
            LocalStorePathResolver.GetSessionsDirectory(_root),
            "session-0638999999999999998-deadbee0.json.gz");

        using (var file = new FileStream(path, FileMode.Create))
        using (var gzip = new GZipStream(file, CompressionLevel.Fastest))
        using (var writer = new StreamWriter(gzip, new UTF8Encoding(false)))
        {
            writer.WriteLine("{ not json either");
        }

        // Act
        store.Delete("Nonexistent.Tests");

        // Assert — the readable session was out of scope, so it survives.
        Assert.Single(store.ReadRecent(10).Sessions);
    }

    [Fact]
    public void ResolveFallsBackToTheProfileWhenTheRepositoryRootIsNotWritable()
    {
        // The documented fallback covers "the root is not writable", but resolution never checked,
        // so a read-only checkout silently lost every run instead of falling back.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return; // chmod semantics differ; the behaviour is verified on Unix CI.

        // Arrange — a repository root that cannot be written to.
        System.Environment.SetEnvironmentVariable(
            LocalStorePathResolver.EnvironmentVariableName, null);

        string repo = Path.Combine(_root, "readonly-repo");
        Directory.CreateDirectory(Path.Combine(repo, ".git"));
        string start = Path.Combine(repo, "tests", "bin");
        Directory.CreateDirectory(start);

        File.SetUnixFileMode(
            repo,
            UnixFileMode.UserRead | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherExecute);

        try
        {
            // Act
            string? resolved = LocalStorePathResolver.Resolve(start);

            // Assert — not the unwritable repository root.
            Assert.NotNull(resolved);
            Assert.NotEqual(
                Path.Combine(repo, LocalStorePathResolver.StoreDirectoryName), resolved);
        }
        finally
        {
            File.SetUnixFileMode(
                repo,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    [Fact]
    public void ResolveStillPrefersAWritableRepositoryRoot()
    {
        System.Environment.SetEnvironmentVariable(
            LocalStorePathResolver.EnvironmentVariableName, null);

        string repo = Path.Combine(_root, "writable-repo");
        Directory.CreateDirectory(Path.Combine(repo, ".git"));
        string start = Path.Combine(repo, "tests", "bin");
        Directory.CreateDirectory(start);

        Assert.Equal(
            Path.Combine(repo, LocalStorePathResolver.StoreDirectoryName),
            LocalStorePathResolver.Resolve(start));
    }

    [Fact]
    public void ASessionRecordsTheModeItWasWrittenUnder()
    {
        // The CLI suppresses the signup invitation for existing customers, which it can only do if
        // the stored session says which mode recorded it.
        JsonSessionStore store = CreateStore();
        store.Write(BuildSession(customProperties: new Dictionary<string, string>
        {
            [LocalSessionProperties.Mode] = nameof(XpingMode.Connected)
        }));

        Assert.True(LocalSessionProperties.IsConnected(store.ReadRecent(1).Sessions[0]));
    }

    [Fact]
    public void SessionsWrittenBeforeTheModePropertyExistedReadAsLocal()
    {
        // Forward compatibility in reverse: a session with no mode property must still read, and
        // must read as not connected — the flag only ever suppresses output.
        JsonSessionStore store = CreateStore();
        store.Write(BuildSession());

        Assert.False(LocalSessionProperties.IsConnected(store.ReadRecent(1).Sessions[0]));
    }
}

public sealed class XpingModeValidationTests
{
    [Fact]
    public void UndefinedModeIsRejectedByValidation()
    {
        // Mode is externally bindable (Xping:Mode=99), and an out-of-range value previously passed
        // validation and then selected a no-op uploader with local-only suppression left off.
        var config = new XpingConfiguration
        {
            ApiKey = "key",
            ProjectId = "proj",
            Mode = (XpingMode)99
        };

        Assert.Contains(config.Validate(), e => e.Contains("undefined", StringComparison.Ordinal));
    }

    [Fact]
    public void UndefinedModeResolvesThroughAutoRatherThanBeingHonoured()
    {
        var config = new XpingConfiguration
        {
            ApiKey = "key",
            ProjectId = "proj",
            Mode = (XpingMode)99
        };

        // Credentials are present, so Auto resolution lands on Connected — a real mode, not the
        // undefined value.
        Assert.Equal(XpingMode.Connected, config.ResolveMode());
    }

    [Fact]
    public void UndefinedModeWithoutCredentialsResolvesToLocalOnly()
    {
        var config = new XpingConfiguration { Mode = (XpingMode)(-3) };

        Assert.Equal(XpingMode.LocalOnly, config.ResolveMode());
    }

    [Theory]
    [InlineData(XpingMode.Auto)]
    [InlineData(XpingMode.LocalOnly)]
    [InlineData(XpingMode.Connected)]
    [InlineData(XpingMode.Disabled)]
    public void DefinedModesPassValidation(XpingMode mode)
    {
        var config = new XpingConfiguration
        {
            ApiKey = "key",
            ProjectId = "proj",
            Mode = mode
        };

        Assert.DoesNotContain(config.Validate(), e => e.Contains("undefined", StringComparison.Ordinal));
    }
}
