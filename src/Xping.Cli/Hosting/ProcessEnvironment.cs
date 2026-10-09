/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Services.Environment;

namespace Xping.Cli.Hosting;

/// <summary>
/// Reads the environment variables of this process.
/// </summary>
/// <remarks>
/// The auth commands read <c>CI</c>, the SSH and display variables and the <c>XPING_*</c> settings
/// through this seam, so a test decides them instead of inheriting the runner's (which sets
/// <c>CI=true</c>).
/// </remarks>
internal sealed class ProcessEnvironment : IEnvironmentVariableProvider
{
    public string? GetVariable(string name) => Environment.GetEnvironmentVariable(name);
}
