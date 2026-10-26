/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using Xping.Sdk.Core;
using Xping.Sdk.Core.Services.LocalStore;

namespace Xping.Sdk.Core.Tests.Orchestration;

/// <summary>
/// Covers how the orchestrator works out the runner and project path it stamps on a local session.
/// </summary>
public sealed class RecordingPropertiesTests
{
    private const string ThisProjectPath = "tests/Xping.Sdk.Core.Tests/Xping.Sdk.Core.Tests.csproj";

    private static readonly string ThisAssemblyName = typeof(RecordingPropertiesTests).Assembly.GetName().Name!;

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
    public void DetectProcessRunner_WhenTheEntryAssemblyCannotBeInspected_ReturnsNull()
    {
        Assert.Null(XpingContextOrchestrator.DetectProcessRunner(() => throw new NotSupportedException()));
    }

    [Fact]
    public void DetectProcessRunner_ForAnEntryAssemblyThatIsNoKnownRunner_ReturnsNull()
    {
        Assert.Null(XpingContextOrchestrator.DetectProcessRunner(() => typeof(XpingContextOrchestrator).Assembly));
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

    // This test project imports the targets the package ships, so its own assembly carries a real stamp.
    [Fact]
    public void ReadStampedProjectPath_ForAStampedAssembly_ReturnsItsProjectPath()
    {
        Assert.Equal(
            ThisProjectPath,
            XpingContextOrchestrator.ReadStampedProjectPath(typeof(RecordingPropertiesTests).Assembly));
    }

    [Fact]
    public void ResolveSessionProjectPath_FindsTheStampAmongTheLoadedAssemblies()
    {
        Assert.Equal(
            ThisProjectPath,
            XpingContextOrchestrator.ResolveSessionProjectPath(
                [ThisAssemblyName], AppDomain.CurrentDomain.GetAssemblies, NullLogger.Instance));
    }

    [Fact]
    public void ResolveSessionProjectPath_ForATestAssemblyThatIsNotLoaded_ReturnsNull()
    {
        Assert.Null(XpingContextOrchestrator.ResolveSessionProjectPath(
            ["Not.Loaded"], AppDomain.CurrentDomain.GetAssemblies, NullLogger.Instance));
    }

    [Fact]
    public void ResolveSessionProjectPath_WhenReadingTheAssembliesFails_ReturnsNull()
    {
        Assert.Null(XpingContextOrchestrator.ResolveSessionProjectPath(
            [ThisAssemblyName], () => throw new InvalidOperationException(), NullLogger.Instance));
    }

    [Fact]
    public void ReadStampedProjectPath_ForAnAssemblyBuiltWithoutTheStamp_ReturnsNull()
    {
        Assert.Null(XpingContextOrchestrator.ReadStampedProjectPath(typeof(XpingContextOrchestrator).Assembly));
    }
}
