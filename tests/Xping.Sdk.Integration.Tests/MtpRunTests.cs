/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Integration.Tests;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Xping.Sdk.Integration.Tests.Infrastructure;
using Xunit;

/// <summary>
/// Runs the NUnit and MSTest samples on Microsoft.Testing.Platform, through <c>dotnet test</c> and as
/// their own executable, uploading to a <see cref="MockApiServer"/>: every test is recorded, written
/// to the local store and uploaded, with no vstest setting.
/// </summary>
[Collection(SampleRunner.Collection)]
public sealed class MtpRunTests : IDisposable
{
    private readonly SampleRunner _runner = new();
    private readonly MockApiServer _server = new();

    public void Dispose()
    {
        _server.Dispose();
        _runner.Dispose();
    }

    // A pass, a thrown exception, and a test its framework cancels for overrunning a 500 ms budget.
    // MSTest leaves its timeout out: it records one as notExecuted on either runner (#289).
    [Theory]
    [InlineData("SampleApp.NUnit", TestRunner.MtpDotnetTest, "PassingTestIsTracked", "ThrowingTestIsTracked", "TimeoutTest_UnresponsiveDependency_AwaitsForever")]
    [InlineData("SampleApp.NUnit", TestRunner.MtpExecutable, "PassingTestIsTracked", "ThrowingTestIsTracked", "TimeoutTest_UnresponsiveDependency_AwaitsForever")]
    [InlineData("SampleApp.MSTest", TestRunner.MtpDotnetTest, "Add_TwoNumbers_ReturnsSum", "ThrowingTestIsTracked", null)]
    [InlineData("SampleApp.MSTest", TestRunner.MtpExecutable, "Add_TwoNumbers_ReturnsSum", "ThrowingTestIsTracked", null)]
    public async Task EveryTestIsRecordedStoredAndUploaded(
        string sample, TestRunner runner, string passing, string throwing, string? timingOut)
    {
        string[] tests = timingOut is null ? [passing, throwing] : [passing, throwing, timingOut];

        // FullyQualifiedName rather than Name: MSTest matches Name against the display name, which
        // [TestMethod("…")] replaces.
        string filter = string.Join("|", tests.Select(test => $"FullyQualifiedName~.{test}"));

        (int exitCode, string output) = await _runner
            .RunAsync(sample, runner, filter, CloudSettings())
            .ConfigureAwait(true);

        // Microsoft.Testing.Platform's exit code for "at least one test failed": the run's own failures,
        // and nothing from Xping.
        exitCode.Should().Be(2, "Output:\n{0}", output);

        Dictionary<string, JsonElement> uploaded = UploadedExecutions();
        uploaded.Keys.Should().BeEquivalentTo(tests, "Output:\n{0}", output);

        uploaded[passing].GetProperty("outcome").GetString().Should().Be("passed");

        JsonElement failed = uploaded[throwing];
        failed.GetProperty("outcome").GetString().Should().Be("failed");
        failed.GetProperty("exceptionType").GetString().Should().Be(typeof(InvalidOperationException).FullName);
        failed.GetProperty("errorMessage").GetString().Should().Contain("This is a test exception for tracking purposes.");
        failed.GetProperty("stackTrace").GetString().Should().Contain(throwing);

        if (timingOut is not null)
        {
            JsonElement timedOut = uploaded[timingOut];
            timedOut.GetProperty("outcome").GetString().Should().Be("timeout");
            TimeSpan.Parse(timedOut.GetProperty("duration").GetString()!, CultureInfo.InvariantCulture)
                .Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(450));
        }

        _server.ReceivedRequests.Should().Contain(
            request => ReadBody(request).GetProperty("sessionState").GetString() == "finalized",
            "the session is finalized. Output:\n{0}", output);

        StoredExecutionNames().Should().BeEquivalentTo(tests, "Output:\n{0}", output);
    }

    // On vstest the safety net rarely gets its upload out: the test host is killed 100 ms after the run
    // unless VSTEST_TESTHOST_SHUTDOWN_TIMEOUT is raised (#126). Microsoft.Testing.Platform runs in the
    // test process and lets ProcessExit handlers finish, so the upload goes through with no setting.
    [Theory]
    [InlineData(TestRunner.MtpDotnetTest)]
    [InlineData(TestRunner.MtpExecutable)]
    public async Task MSTestWithoutAssemblyCleanup_SafetyNetFinalizesAndUploads(TestRunner runner)
    {
        (int exitCode, string output) = await _runner
            .RunAsync("SampleApp.MSTest", runner, "FullyQualifiedName~.Add_TwoNumbers_ReturnsSum", CloudSettings(), "XpingSkipAssemblyCleanup=true")
            .ConfigureAwait(true);

        // With no [AssemblyCleanup], only the safety net finalizes: the finalized upload is its work.
        // dotnet test does not show a passing app's output, so its warning cannot be checked here.
        exitCode.Should().Be(0, "Output:\n{0}", output);
        UploadedExecutions().Keys.Should().Equal(["Add_TwoNumbers_ReturnsSum"], "Output:\n{0}", output);
        _server.ReceivedRequests.Should().Contain(
            request => ReadBody(request).GetProperty("sessionState").GetString() == "finalized",
            "the safety net finalizes the session. Output:\n{0}", output);
        StoredExecutionNames().Should().Equal(["Add_TwoNumbers_ReturnsSum"], "Output:\n{0}", output);
    }

    private Dictionary<string, string> CloudSettings() => new()
    {
        ["XPING_MODE"] = "Cloud",
        ["XPING_APIKEY"] = "test-key",
        ["XPING_PROJECTID"] = "test-project",
        ["XPING_APIENDPOINT"] = _server.BaseUrl + "v1",
        ["XPING_MAXRETRIES"] = "0",
    };

    private Dictionary<string, JsonElement> UploadedExecutions() => _server.ReceivedRequests
        .SelectMany(request => ReadBody(request).TryGetProperty("executions", out JsonElement executions)
            ? executions.EnumerateArray()
            : Enumerable.Empty<JsonElement>())
        .ToDictionary(MethodName);

    private string[] StoredExecutionNames()
    {
        string session = Directory.GetFiles(Path.Combine(_runner.StorePath, "sessions"), "*.json.gz")
            .Should().ContainSingle().Subject;

        using var gzip = new GZipStream(File.OpenRead(session), CompressionMode.Decompress);
        using JsonDocument document = JsonDocument.Parse(gzip);
        return document.RootElement.GetProperty("executions").EnumerateArray()
            .Select(MethodName)
            .ToArray();
    }

    // Not testName, which MSTest takes from the display name.
    private static string MethodName(JsonElement execution) =>
        execution.GetProperty("identity").GetProperty("methodName").GetString()!;

    private static JsonElement ReadBody(ReceivedRequest request)
    {
        using JsonDocument document = JsonDocument.Parse(request.Body ?? "{}");
        return document.RootElement.Clone();
    }
}
