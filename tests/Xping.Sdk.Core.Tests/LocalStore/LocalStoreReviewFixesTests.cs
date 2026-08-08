/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Models.Local;
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

    private static LocalRun BuildRun(string name = "T") =>
        new(
            new LocalRunHeader { SessionId = Guid.NewGuid().ToString("N"), StartedAtUtc = DateTime.UtcNow },
            [new LocalTestRecord { Fingerprint = "fp", Name = name, Outcome = OutcomeCodes.Passed }]);

    [Fact]
    public void AMalformedHeaderSkipsOnlyThatRun()
    {
        // Arrange — a corrupt header threw JsonException past the catch, aborting the entire read.
        // One bad file must cost one run, which is what the store contract promises.
        var store = LocalRunStore.Create();
        store.Write(BuildRun("good"));

        string path = Path.Combine(
            LocalStorePathResolver.GetRunsDirectory(_root),
            "run-0638999999999999999-deadbeef.jsonl.gz");

        using (var file = new FileStream(path, FileMode.Create))
        using (var gzip = new GZipStream(file, CompressionLevel.Fastest))
        using (var writer = new StreamWriter(gzip, new UTF8Encoding(false)))
        {
            writer.WriteLine("{ this is not valid json");
            writer.WriteLine("{\"f\":\"fp\",\"n\":\"A\",\"o\":\"P\"}");
        }

        // Act
        var runs = store.ReadRecent(10);

        // Assert
        var run = Assert.Single(runs);
        Assert.Equal("good", run.Records[0].Name);
    }

    [Fact]
    public void AMalformedHeaderDoesNotBreakDelete()
    {
        // Arrange
        var store = LocalRunStore.Create();
        store.Write(BuildRun("good"));

        string path = Path.Combine(
            LocalStorePathResolver.GetRunsDirectory(_root),
            "run-0638999999999999998-deadbee0.jsonl.gz");

        using (var file = new FileStream(path, FileMode.Create))
        using (var gzip = new GZipStream(file, CompressionLevel.Fastest))
        using (var writer = new StreamWriter(gzip, new UTF8Encoding(false)))
        {
            writer.WriteLine("{ not json either");
        }

        // Act & Assert — a scoped delete reads headers, so it must survive a corrupt one.
        store.Delete("Nonexistent.Tests");
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
    public void RunHeaderRecordsConnectedStatus()
    {
        // The CLI suppresses the signup invitation for existing customers, which it can only do if
        // the store says whether the run was connected.
        var store = LocalRunStore.Create();
        var run = new LocalRun(
            new LocalRunHeader
            {
                SessionId = "s",
                StartedAtUtc = DateTime.UtcNow,
                IsConnected = true
            },
            [new LocalTestRecord { Fingerprint = "fp", Name = "T", Outcome = OutcomeCodes.Passed }]);

        store.Write(run);

        Assert.True(store.ReadRecent(1)[0].Header.IsConnected);
    }

    [Fact]
    public void RunsWrittenBeforeConnectedStatusExistedReadAsLocal()
    {
        // Forward compatibility in reverse: a file without the field must not fail to parse.
        var store = LocalRunStore.Create();
        string path = Path.Combine(
            LocalStorePathResolver.GetRunsDirectory(_root),
            "run-0638000000000000000-abcdef01.jsonl.gz");
        Directory.CreateDirectory(LocalStorePathResolver.GetRunsDirectory(_root));

        using (var file = new FileStream(path, FileMode.Create))
        using (var gzip = new GZipStream(file, CompressionLevel.Fastest))
        using (var writer = new StreamWriter(gzip, new UTF8Encoding(false)))
        {
            writer.WriteLine("{\"v\":1,\"sid\":\"old\",\"ts\":\"2026-01-01T00:00:00Z\"}");
            writer.WriteLine("{\"f\":\"fp\",\"n\":\"A\",\"o\":\"P\"}");
        }

        Assert.False(store.ReadRecent(1)[0].Header.IsConnected);
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
