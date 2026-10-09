/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Hosting;
using Xping.Sdk.Core.Services.Environment;

namespace Xping.Cli.Configuration;

/// <summary>
/// Resolves the <see cref="CliConfiguration"/> of this invocation from the parsed global options,
/// the environment and the settings files of a directory.
/// </summary>
internal sealed class CliConfigurationLoader(GlobalOptions options, IEnvironmentVariableProvider environment)
{
    /// <summary>
    /// Loads the configuration.
    /// </summary>
    /// <exception cref="CliConfigurationException">
    /// A settings file cannot be parsed, or a configured Cloud URL is invalid.
    /// </exception>
    public CliConfiguration Load() => Load(Directory.GetCurrentDirectory(), projectFlag: null);

    /// <summary>
    /// Loads the configuration with the settings files of <paramref name="directory"/>.
    /// </summary>
    /// <param name="directory">Where <c>appsettings*.json</c> are looked for: the directory a report is about.</param>
    /// <param name="projectFlag">The <c>report --project</c> value, if given.</param>
    /// <exception cref="CliConfigurationException">
    /// A settings file cannot be parsed, or a configured Cloud URL is invalid.
    /// </exception>
    public CliConfiguration Load(string directory, string? projectFlag) =>
        CliConfiguration.Load(options.CloudUrl, options.ApiKey, projectFlag, directory, environment.GetVariable);
}
