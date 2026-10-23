/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Reflection;
using Xping.Sdk.Core.Exceptions;

namespace Xping.Sdk.Core.Tests.Orchestration;

/// <summary>
/// Tests for how <c>XpingContextOrchestrator</c> fails the process on Microsoft.Testing.Platform, and
/// how it tells that runner apart.
/// </summary>
public sealed class XpingContextOrchestratorRunFailureTests
{
    [Fact]
    public void FailExitCode_ReportsTheErrorAndSetsExitCodeOne()
    {
        int exitCode = Environment.ExitCode;
        using var errors = new StringWriter();
        try
        {
            XpingContextOrchestrator.FailExitCode(new XpingNetworkException("upload refused"), errors);

            Assert.Equal(1, Environment.ExitCode);
            Assert.Contains("[Xping] upload refused", errors.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            // This test process's own exit code: the run must not fail on it.
            Environment.ExitCode = exitCode;
        }
    }

    [Fact]
    public void ReferencesTestingPlatform_WithThePlatformAmongThem_ReturnsTrue()
    {
        AssemblyName[] references = [new("System.Runtime"), new("Microsoft.Testing.Platform")];

        Assert.True(XpingContextOrchestrator.ReferencesTestingPlatform(references));
    }

    [Fact]
    public void ReferencesTestingPlatform_WithOnlyItsExtensions_ReturnsFalse()
    {
        AssemblyName[] references = [new("System.Runtime"), new("Microsoft.Testing.Platform.MSBuild")];

        Assert.False(XpingContextOrchestrator.ReferencesTestingPlatform(references));
    }

    [Fact]
    public void RunsOnTestingPlatform_WithoutEntryAssembly_ReturnsFalse()
    {
        Assert.False(XpingContextOrchestrator.RunsOnTestingPlatform(null));
    }

    [Fact]
    public void RunsOnTestingPlatform_ForAnAssemblyThatIsNoPlatformApp_ReturnsFalse()
    {
        Assert.False(XpingContextOrchestrator.RunsOnTestingPlatform(typeof(XpingContextOrchestrator).Assembly));
    }
}
