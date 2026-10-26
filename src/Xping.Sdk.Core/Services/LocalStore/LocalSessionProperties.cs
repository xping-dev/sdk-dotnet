/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Models;

namespace Xping.Sdk.Core.Services.LocalStore;

/// <summary>
/// Environment properties the local store adds to the sessions it writes.
/// </summary>
/// <remarks>
/// These describe how a session was recorded rather than what it recorded, so they travel on
/// <see cref="Xping.Sdk.Core.Models.Environments.EnvironmentInfo.CustomProperties"/> beside the Git
/// metadata rather than as fields on <see cref="TestSession"/> itself. The session model is also the
/// upload payload; adding a field there to answer a purely local question would change the wire
/// format for every connected project.
/// </remarks>
public static class LocalSessionProperties
{
    /// <summary>
    /// The property key holding the <see cref="XpingMode"/> the session was recorded under.
    /// </summary>
    public const string Mode = "Xping.Mode";

    /// <summary>
    /// The property key holding the project id the session was pinned to with
    /// <see cref="XpingConfiguration.ProjectId"/>. Absent when no project was pinned.
    /// </summary>
    /// <remarks>
    /// The Cloud derives a project key from the assembly name unless a pin is set, and the CLI must
    /// not reproduce that derivation. A pinned key is the one value the CLI cannot learn any other
    /// way, so it is recorded with the session the CLI later reads.
    /// </remarks>
    public const string ProjectId = "Xping.ProjectId";

    /// <summary>
    /// The property key holding the test runner the session ran on: <see cref="VSTestRunner"/> or
    /// <see cref="TestingPlatformRunner"/>. Absent when the SDK could not tell.
    /// </summary>
    /// <remarks>
    /// <c>dotnet test</c> takes different arguments on each runner, so a command the CLI prints to run
    /// a test again depends on it. Left out rather than guessed: a wrong runner prints a command that
    /// fails, where a missing one prints none. <see cref="TestingPlatformRunner"/> covers both ways
    /// to start a Microsoft.Testing.Platform app, <c>dotnet test</c> and the test executable itself;
    /// they take the same filter arguments.
    /// </remarks>
    public const string Runner = "Xping.Runner";

    /// <summary>
    /// The property key holding the test framework whose adapter recorded the session:
    /// <see cref="NUnitFramework"/>, <see cref="MSTestFramework"/> or <see cref="XUnitFramework"/>.
    /// </summary>
    public const string TestFramework = "Xping.TestFramework";

    /// <summary>
    /// The property key holding the path of the test project, relative to the repository root and with
    /// <c>/</c> separators, e.g. <c>tests/Shop.Tests/Shop.Tests.csproj</c>.
    /// </summary>
    /// <remarks>
    /// Stamped into the test assembly at build time by the <c>Xping.Sdk.Core.targets</c> file the
    /// package ships. Absent when the project was not built in a repository, was built with
    /// <c>GenerateAssemblyInfo=false</c>, or when the session's test assemblies name different
    /// projects.
    /// </remarks>
    public const string ProjectPath = "Xping.ProjectPath";

    /// <summary>The <see cref="Runner"/> value for a run in a vstest test host.</summary>
    public const string VSTestRunner = "VSTest";

    /// <summary>The <see cref="Runner"/> value for a run on Microsoft.Testing.Platform.</summary>
    public const string TestingPlatformRunner = "MTP";

    /// <summary>The <see cref="TestFramework"/> value for the NUnit adapter.</summary>
    public const string NUnitFramework = "NUnit";

    /// <summary>The <see cref="TestFramework"/> value for the MSTest adapter.</summary>
    public const string MSTestFramework = "MSTest";

    /// <summary>The <see cref="TestFramework"/> value for the xUnit v2 adapter.</summary>
    public const string XUnitFramework = "xUnit";

    /// <summary>
    /// Returns whether the session was recorded by a cloud-connected project.
    /// </summary>
    /// <param name="session">The session to inspect, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the session was recorded in Cloud mode.</returns>
    /// <remarks>
    /// Sessions written before this property existed carry no key and read as not connected. That is
    /// the safe direction: the flag only ever suppresses output, so an old session at worst shows a
    /// developer something they have already seen.
    /// </remarks>
    public static bool IsConnected(TestSession? session)
    {
        if (session?.EnvironmentInfo?.CustomProperties == null)
            return false;

        return session.EnvironmentInfo.CustomProperties.TryGetValue(Mode, out string? mode) &&
               string.Equals(mode, nameof(XpingMode.Cloud), StringComparison.Ordinal);
    }
}
