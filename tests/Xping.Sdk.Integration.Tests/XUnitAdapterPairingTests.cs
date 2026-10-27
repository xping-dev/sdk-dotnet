/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Integration.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xping.Sdk.Integration.Tests.Infrastructure;
using Xunit;

/// <summary>
/// Checks the MSBuild file each xUnit adapter ships to stop a build that pairs it with the other xUnit
/// version, and that the packages carry it.
/// </summary>
/// <remarks>
/// Without it, either wrong pairing fails on every <c>[Fact]</c> with CS0433, because xUnit v2's
/// <c>xunit.core</c> and v3's <c>xunit.v3.core</c> define the same <c>Xunit</c> types, and nothing in
/// that error names the package to swap. Both right pairings build with these files imported in the
/// samples, so a check that fired on them would fail every sample run.
/// </remarks>
[Collection(SampleRunner.Collection)]
public sealed class XUnitAdapterPairingTests : IDisposable
{
    private static readonly Dictionary<string, string> NoSettings = [];

    private readonly SampleRunner _runner = new();
    private readonly string _repositoryRoot = SampleRunner.FindRepositoryRoot();

    public void Dispose() => _runner.Dispose();

    [Theory]
    [InlineData("Xping.Sdk.XUnit", "xunit.v3", "4.0.2", "XPING001", "Replace it with Xping.Sdk.XUnit.V3.")]
    [InlineData("Xping.Sdk.XUnit.V3", "xunit", "2.9.2", "XPING002", "Replace it with Xping.Sdk.XUnit.")]
    public async Task AnAdapterPairedWithTheOtherXunitVersion_StopsTheBuildNamingThePackageToUse(
        string adapter, string xunitPackage, string xunitVersion, string code, string advice)
    {
        string project = await WriteProjectAsync(adapter, xunitPackage, xunitVersion).ConfigureAwait(true);

        (int exitCode, string output) = await _runner.DotnetAsync(
            Path.GetDirectoryName(project)!,
            ["build", project],
            NoSettings,
            $"build of {adapter} with {xunitPackage}").ConfigureAwait(true);

        exitCode.Should().NotBe(0, "Output:\n{0}", output);
        output.Should().Contain($"error {code}: {adapter} supports xUnit").And.Contain(advice);
        output.Should().NotContain("CS0433", "the build stops before the compiler reports the clash");
    }

    [Theory]
    [InlineData("Xping.Sdk.XUnit")]
    [InlineData("Xping.Sdk.XUnit.V3")]
    public async Task TheAdapterPackageShipsTheCheckAsBuildTransitive(string adapter)
    {
        string output = Path.Combine(_runner.ScratchPath, "packages");

        (int exitCode, string log) = await _runner.DotnetAsync(
            _repositoryRoot,
            ["pack", Path.Combine("src", adapter), "--configuration", "Release", "--output", output],
            NoSettings,
            $"dotnet pack of {adapter}").ConfigureAwait(true);

        exitCode.Should().Be(0, "Output:\n{0}", log);
        string package = Directory.GetFiles(output, $"{adapter}.1*.nupkg")
            .Where(path => !path.EndsWith(".snupkg", StringComparison.Ordinal))
            .Should().ContainSingle().Subject;

        using ZipArchive archive = await ZipFile.OpenReadAsync(package, CancellationToken.None).ConfigureAwait(true);
        archive.Entries.Select(entry => entry.FullName).Should().Contain($"buildTransitive/{adapter}.targets");
    }

    /// <summary>
    /// A test project outside the repository that references the adapter, and imports the adapter's
    /// targets as NuGet would from its buildTransitive folder.
    /// </summary>
    private async Task<string> WriteProjectAsync(string adapter, string xunitPackage, string xunitVersion)
    {
        string directory = Path.Combine(_runner.ScratchPath, "Shop.Tests");
        Directory.CreateDirectory(directory);
        directory = ResolveLinks(directory);

        string adapterProject = Path.Combine(_repositoryRoot, "src", adapter, $"{adapter}.csproj");
        string targets = Path.Combine(_repositoryRoot, "src", adapter, "buildTransitive", $"{adapter}.targets");
        string project = Path.Combine(directory, "Shop.Tests.csproj");

        // xUnit v3 test projects are executables; xunit.v3 fails the build of one that is not.
        string outputType = xunitPackage == "xunit.v3" ? "<OutputType>Exe</OutputType>" : string.Empty;

        await File.WriteAllTextAsync(
            project,
            $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                {outputType}
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="{xunitPackage}" Version="{xunitVersion}" />
                <ProjectReference Include="{adapterProject}" />
              </ItemGroup>
              <Import Project="{targets}" />
            </Project>
            """).ConfigureAwait(true);

        await File.WriteAllTextAsync(
            Path.Combine(directory, "CartTests.cs"),
            """
            public class CartTests
            {
                [Xunit.Fact]
                public void Adds() { }
            }
            """).ConfigureAwait(true);

        return project;
    }

    /// <summary>
    /// Resolves the symbolic links in a path, as macOS's temporary folder under <c>/var</c> has one.
    /// </summary>
    /// <remarks>
    /// MSBuild writes the adapter's own project references relative to the real path, and following
    /// <c>..</c> out of a link lands somewhere else, so the build would not find Xping.Sdk.Core.
    /// </remarks>
    private static string ResolveLinks(string path)
    {
        for (DirectoryInfo? directory = new(path); directory != null; directory = directory.Parent)
        {
            if (directory.LinkTarget != null)
            {
                string target = directory.ResolveLinkTarget(returnFinalTarget: true)!.FullName;
                return Path.Combine(target, Path.GetRelativePath(directory.FullName, path));
            }
        }

        return path;
    }
}
