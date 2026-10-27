/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Integration.Tests;

using System;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Xping.Sdk.Core.Services.LocalStore;
using Xping.Sdk.Integration.Tests.Infrastructure;
using Xunit;

/// <summary>
/// Runs each sample on each runner it supports and checks the session it stores says how it ran: the
/// runner, the test framework and the project, which the CLI needs to print a command that runs a
/// test again.
/// </summary>
[Collection(SampleRunner.Collection)]
public sealed class SessionPropertiesRunTests : IDisposable
{
    private readonly SampleRunner _runner = new();

    public void Dispose() => _runner.Dispose();

    [Theory]
    [InlineData("SampleApp.NUnit", TestRunner.VsTest, "FullyQualifiedName~.PassingTestIsTracked", "VSTest", "NUnit")]
    [InlineData("SampleApp.NUnit", TestRunner.MtpDotnetTest, "FullyQualifiedName~.PassingTestIsTracked", "MTP", "NUnit")]
    [InlineData("SampleApp.NUnit", TestRunner.MtpExecutable, "FullyQualifiedName~.PassingTestIsTracked", "MTP", "NUnit")]
    [InlineData("SampleApp.MSTest", TestRunner.VsTest, "FullyQualifiedName~.Add_TwoNumbers_ReturnsSum", "VSTest", "MSTest")]
    [InlineData("SampleApp.MSTest", TestRunner.MtpDotnetTest, "FullyQualifiedName~.Add_TwoNumbers_ReturnsSum", "MTP", "MSTest")]
    [InlineData("SampleApp.MSTest", TestRunner.MtpExecutable, "FullyQualifiedName~.Add_TwoNumbers_ReturnsSum", "MTP", "MSTest")]
    [InlineData("SampleApp.XUnit", TestRunner.VsTest, "FullyQualifiedName~.PassingTestIsTracked", "VSTest", "xUnit")]
    public async Task TheStoredSessionNamesItsRunnerFrameworkAndProject(
        string sample, TestRunner runner, string filter, string expectedRunner, string expectedFramework)
    {
        (int exitCode, string output) = await _runner.RunAsync(sample, runner, filter, SampleRunner.LocalOnly).ConfigureAwait(true);

        exitCode.Should().Be(0, "Output:\n{0}", output);

        JsonElement properties = CustomProperties();
        properties.GetProperty(LocalSessionProperties.Runner).GetString().Should().Be(expectedRunner);
        properties.GetProperty(LocalSessionProperties.TestFramework).GetString().Should().Be(expectedFramework);
        properties.GetProperty(LocalSessionProperties.ProjectPath).GetString()
            .Should().Be($"samples/{sample}/{sample}.csproj");
    }

    [Fact]
    public async Task AProjectBuiltWithoutAssemblyInfoRecordsNoProjectPath()
    {
        (int exitCode, string output) = await _runner
            .RunAsync("SampleApp.NUnit", TestRunner.VsTest, "FullyQualifiedName~.PassingTestIsTracked", SampleRunner.LocalOnly, "XpingSampleWithoutAssemblyInfo=true")
            .ConfigureAwait(true);

        exitCode.Should().Be(0, "Output:\n{0}", output);
        CustomProperties().TryGetProperty(LocalSessionProperties.ProjectPath, out _).Should().BeFalse();
    }

    private JsonElement CustomProperties() =>
        _runner.ReadStoredSession().GetProperty("environmentInfo").GetProperty("customProperties");
}
