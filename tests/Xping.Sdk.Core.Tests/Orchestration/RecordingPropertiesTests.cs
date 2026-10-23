/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Reflection;
using Xping.Sdk.Core;
using Xping.Sdk.Core.Services.LocalStore;

namespace Xping.Sdk.Core.Tests.Orchestration;

/// <summary>
/// Covers how the orchestrator works out the runner and project path it stamps on a local session.
/// </summary>
public sealed class RecordingPropertiesTests
{
    [Theory]
    [InlineData("testhost")]
    [InlineData("testhost.x86")]
    [InlineData("testhost.net48")]
    public void DetectRunner_InAVSTestHost_ReturnsVSTest(string entryName)
    {
        Assert.Equal(
            LocalSessionProperties.VSTestRunner,
            XpingContextOrchestrator.DetectRunner(entryName, [new("System.Runtime")]));
    }

    [Fact]
    public void DetectRunner_ForAnEntryAssemblyReferencingThePlatform_ReturnsMTP()
    {
        AssemblyName[] references = [new("System.Runtime"), new("Microsoft.Testing.Platform")];

        Assert.Equal(
            LocalSessionProperties.TestingPlatformRunner,
            XpingContextOrchestrator.DetectRunner("Shop.Tests", references));
    }

    [Theory]
    [InlineData("xunit.console")]
    [InlineData("testhostess")]
    [InlineData(null)]
    public void DetectRunner_ForAnyOtherHost_ReturnsNull(string? entryName)
    {
        Assert.Null(XpingContextOrchestrator.DetectRunner(entryName, [new("System.Runtime")]));
    }

    [Fact]
    public void DetectRunner_WithoutEntryAssembly_ReturnsNull()
    {
        Assert.Null(XpingContextOrchestrator.DetectRunner(entryAssembly: null));
    }

    [Fact]
    public void ResolveProjectPath_ForOneStampedAssembly_ReturnsItsPath()
    {
        Assert.Equal(
            "tests/Shop.Tests/Shop.Tests.csproj",
            XpingContextOrchestrator.ResolveProjectPath(["Shop.Tests"], _ => "tests/Shop.Tests/Shop.Tests.csproj"));
    }

    [Fact]
    public void ResolveProjectPath_ForAssembliesThatAgree_ReturnsTheSharedPath()
    {
        Assert.Equal(
            "tests/Shop.Tests/Shop.Tests.csproj",
            XpingContextOrchestrator.ResolveProjectPath(["A", "B"], _ => "tests/Shop.Tests/Shop.Tests.csproj"));
    }

    [Fact]
    public void ResolveProjectPath_ForAssembliesFromDifferentProjects_ReturnsNull()
    {
        Assert.Null(XpingContextOrchestrator.ResolveProjectPath(["A", "B"], name => $"tests/{name}/{name}.csproj"));
    }

    [Fact]
    public void ResolveProjectPath_WhenOneAssemblyHasNoPath_ReturnsNull()
    {
        Assert.Null(XpingContextOrchestrator.ResolveProjectPath(
            ["A", "B"], name => name == "A" ? "tests/A/A.csproj" : null));
    }

    [Fact]
    public void ResolveProjectPath_WithoutAssemblies_ReturnsNull()
    {
        Assert.Null(XpingContextOrchestrator.ResolveProjectPath([], _ => "tests/A/A.csproj"));
    }

    [Fact]
    public void ReadStampedProjectPath_ForAnAssemblyThatIsNotLoaded_ReturnsNull()
    {
        Assert.Null(XpingContextOrchestrator.ReadStampedProjectPath(
            [typeof(XpingContextOrchestrator).Assembly], "Not.Loaded"));
    }

    [Fact]
    public void ReadStampedProjectPath_ForAnAssemblyBuiltWithoutTheStamp_ReturnsNull()
    {
        Assembly core = typeof(XpingContextOrchestrator).Assembly;

        Assert.Null(XpingContextOrchestrator.ReadStampedProjectPath([core], core.GetName().Name!));
    }
}
