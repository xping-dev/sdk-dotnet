/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Integration.Tests;

using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Xping.Sdk.Integration.Tests.Infrastructure;
using Xunit;

/// <summary>
/// Runs a failing test in each sample and checks the stored session keeps its stack trace and the
/// trace's hash. Only the upload leaves the trace out (<c>UploadStackTraces</c>); the CLI groups
/// failures by what the store holds.
/// </summary>
[Collection(SampleRunner.Collection)]
public sealed class StackTraceRunTests : IDisposable
{
    private const string Throwing = "ThrowingTestIsTracked";

    private readonly SampleRunner _runner = new();

    public void Dispose() => _runner.Dispose();

    [Theory]
    [InlineData("SampleApp.NUnit")]
    [InlineData("SampleApp.MSTest")]
    [InlineData("SampleApp.XUnit")]
    [InlineData("SampleApp.XUnitV3")]
    public async Task AFailedTestKeepsItsStackTraceAndHashInTheStore(string sample)
    {
        (int exitCode, string output) = await _runner
            .RunAsync(sample, TestRunner.VsTest, $"FullyQualifiedName~.{Throwing}", SampleRunner.LocalOnly)
            .ConfigureAwait(true);

        // dotnet test's exit code for a run with a failed test: the sample's own failure.
        exitCode.Should().Be(1, "Output:\n{0}", output);

        JsonElement failed = _runner.ReadStoredSession().GetProperty("executions").EnumerateArray()
            .Single(execution => execution.GetProperty("identity").GetProperty("methodName").GetString() == Throwing);

        failed.GetProperty("stackTrace").GetString().Should().Contain(Throwing);
        failed.GetProperty("stackTraceHash").GetString().Should().NotBeNullOrEmpty();
        failed.GetProperty("stackTraceOmitted").GetBoolean().Should().BeFalse();
    }
}
