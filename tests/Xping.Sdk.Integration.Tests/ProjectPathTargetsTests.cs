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
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xping.Sdk.Integration.Tests.Infrastructure;
using Xunit;

/// <summary>
/// Checks the MSBuild file Xping.Sdk.Core ships to stamp the project path into a test assembly:
/// what it stamps, when it stamps nothing, and that the package carries it.
/// </summary>
/// <remarks>
/// Evaluated with <c>dotnet msbuild -getItem</c>, which reads the items without building.
/// </remarks>
[Collection(SampleRunner.Collection)]
public sealed class ProjectPathTargetsTests : IDisposable
{
    private static readonly Dictionary<string, string> NoSettings = [];

    private readonly SampleRunner _runner = new();
    private readonly string _repositoryRoot = SampleRunner.FindRepositoryRoot();

    public void Dispose() => _runner.Dispose();

    [Fact]
    public async Task InARepositoryTheProjectPathIsStampedRelativeToItsRoot()
    {
        string project = Path.Combine(_repositoryRoot, "samples", "SampleApp.MSTest", "SampleApp.MSTest.csproj");

        (await StampedProjectPathsAsync(project).ConfigureAwait(true))
            .Should().Equal(["samples/SampleApp.MSTest/SampleApp.MSTest.csproj"]);
    }

    [Fact]
    public async Task OutsideARepositoryNothingIsStamped()
    {
        string directory = Path.Combine(_runner.ScratchPath, "outside");
        Directory.CreateDirectory(directory);
        string targets = Path.Combine(_repositoryRoot, "src", "Xping.Sdk.Core", "buildTransitive", "Xping.Sdk.Core.targets");
        string project = Path.Combine(directory, "Outside.csproj");
        await File.WriteAllTextAsync(
            project,
            $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <Import Project="{targets}" />
            </Project>
            """).ConfigureAwait(true);

        (await StampedProjectPathsAsync(project).ConfigureAwait(true)).Should().BeEmpty();
    }

    [Fact]
    public async Task TheCorePackageShipsTheTargetsAsBuildTransitive()
    {
        string output = Path.Combine(_runner.ScratchPath, "packages");

        (int exitCode, string log) = await _runner.DotnetAsync(
            _repositoryRoot,
            ["pack", Path.Combine("src", "Xping.Sdk.Core"), "--configuration", "Release", "--output", output],
            NoSettings,
            "dotnet pack of Xping.Sdk.Core").ConfigureAwait(true);

        exitCode.Should().Be(0, "Output:\n{0}", log);
        string package = Directory.GetFiles(output, "Xping.Sdk.Core.*.nupkg")
            .Where(path => !path.EndsWith(".snupkg", StringComparison.Ordinal))
            .Should().ContainSingle().Subject;

        using ZipArchive archive = await ZipFile.OpenReadAsync(package, CancellationToken.None).ConfigureAwait(true);
        archive.Entries.Select(entry => entry.FullName).Should().Contain("buildTransitive/Xping.Sdk.Core.targets");
    }

    private async Task<string[]> StampedProjectPathsAsync(string project)
    {
        (int exitCode, string output) = await _runner.DotnetAsync(
            Path.GetDirectoryName(project)!,
            ["msbuild", project, "-getItem:AssemblyAttribute"],
            NoSettings,
            $"evaluation of {project}").ConfigureAwait(true);

        exitCode.Should().Be(0, "Output:\n{0}", output);

        // The evaluation prints its JSON alone on stdout; stderr is empty when it succeeds.
        using JsonDocument document = JsonDocument.Parse(output);
        return document.RootElement.GetProperty("Items").GetProperty("AssemblyAttribute").EnumerateArray()
            .Where(item => item.TryGetProperty("_Parameter1", out JsonElement key) && key.GetString() == "Xping.ProjectPath")
            .Select(item => item.GetProperty("_Parameter2").GetString()!)
            .ToArray();
    }
}
