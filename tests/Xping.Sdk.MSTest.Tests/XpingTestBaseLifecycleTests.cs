/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.IO.Compression;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xping.Sdk.Core.Configuration;
using Assert = Xunit.Assert;

namespace Xping.Sdk.MSTest.Tests;

/// <summary>
/// Drives <see cref="XpingTestBase"/> through <c>[TestInitialize]</c> and <c>[TestCleanup]</c> as MSTest
/// would, and reads back the execution the session stores.
/// </summary>
[Collection(XpingContextCollection.Name)]
public sealed class XpingTestBaseLifecycleTests : IAsyncLifetime
{
    private readonly string _scratchStore = Path.Combine(
        Path.GetTempPath(), "xping-tests", Guid.NewGuid().ToString("N"));

    public Task InitializeAsync() => XpingContext.ShutdownAsync().AsTask();

    public async Task DisposeAsync()
    {
        await XpingContext.ShutdownAsync().ConfigureAwait(false);
        if (Directory.Exists(_scratchStore))
            Directory.Delete(_scratchStore, recursive: true);
    }

    /// <summary>
    /// What MSTest hands cleanup for a test [Timeout] abandoned: the outcome still InProgress (#289).
    /// </summary>
    [Fact]
    public async Task ATestAbandonedByItsTimeoutIsStoredAsATimeoutWithItsBudget()
    {
        JsonElement execution = await RunAsync(UnitTestOutcome.InProgress).ConfigureAwait(true);

        Assert.Equal("timeout", execution.GetProperty("outcome").GetString());
        Assert.Equal(TimeSpan.FromMilliseconds(500), TimeSpan.Parse(execution.GetProperty("timeoutBudget").GetString()!, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task APassingTestIsStoredAsPassed()
    {
        JsonElement execution = await RunAsync(UnitTestOutcome.Passed).ConfigureAwait(true);

        Assert.Equal("passed", execution.GetProperty("outcome").GetString());
    }

    private async Task<JsonElement> RunAsync(UnitTestOutcome reported)
    {
        XpingContext.Initialize(new XpingConfiguration { Mode = XpingMode.LocalOnly, LocalStorePath = _scratchStore });

        var test = new TimedTestClass
        {
            TestContext = new LifecycleTestContext(
                nameof(TimedTestClass.TimesOut), typeof(TimedTestClass).FullName!, reported),
        };
        test.XpingTestInitialize();
        test.XpingTestCleanup();

        await XpingContext.FinalizeAndShutdownAsync().ConfigureAwait(true);

        string file = Assert.Single(Directory.GetFiles(_scratchStore, "*.json.gz", SearchOption.AllDirectories));
        using var gzip = new GZipStream(File.OpenRead(file), CompressionMode.Decompress);
        using JsonDocument session = await JsonDocument.ParseAsync(gzip).ConfigureAwait(true);
        return Assert.Single(session.RootElement.GetProperty("executions").EnumerateArray()).Clone();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "MicrosoftCodeAnalysisCorrectness",
        "MSTEST0003:Test method signature is invalid",
        Justification = "Test helper class driven by hand, not run by the MSTest runner")]
    private sealed class TimedTestClass : XpingTestBase
    {
        [TestMethod]
        [Timeout(500)]
        public void TimesOut()
        {
        }
    }

    private sealed class LifecycleTestContext(string testName, string fullClassName, UnitTestOutcome outcome) : TestContext
    {
        // MSTest 3.8 made this abstract; the doubles have no console to display on.
        public override void DisplayMessage(MessageLevel messageLevel, string message) { }

#pragma warning disable CS8609 // Nullability of reference types in return type doesn't match overridden member
        public override System.Collections.IDictionary Properties { get; } = new Dictionary<string, object?>();
#pragma warning restore CS8609

        public override string TestName => testName;

        public override string? FullyQualifiedTestClassName => fullClassName;

        public override UnitTestOutcome CurrentTestOutcome => outcome;

        public override void AddResultFile(string fileName) { }

        public override void WriteLine(string? message) { }

        public override void WriteLine(string format, params object?[]? args) { }

        public override void Write(string? message) { }

        public override void Write(string format, params object?[]? args) { }
    }
}
