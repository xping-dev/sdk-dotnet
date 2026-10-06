/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Hosting;
using Xping.Sdk.Core.Services.Environment;

namespace Xping.Cli.Configuration;

/// <summary>
/// Resolves the <see cref="CliConfiguration"/> of this invocation from the parsed global options,
/// the environment and the working directory.
/// </summary>
internal sealed class CliConfigurationLoader(GlobalOptions options, IEnvironmentVariableProvider environment)
{
    /// <summary>
    /// Loads the configuration.
    /// </summary>
    /// <exception cref="CliConfigurationException">
    /// A settings file cannot be parsed, or a configured Cloud URL is invalid.
    /// </exception>
    public CliConfiguration Load() =>
        CliConfiguration.Load(options.CloudUrl, options.ApiKey, Directory.GetCurrentDirectory(), environment.GetVariable);
}
