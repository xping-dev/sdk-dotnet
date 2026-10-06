/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.Configuration;

namespace Xping.Cli.Configuration;

/// <summary>
/// Where a configured value came from.
/// </summary>
internal enum ConfigurationSource
{
    /// <summary>A command-line option.</summary>
    Flag,

    /// <summary>An <c>XPING_*</c> or <c>Xping__*</c> environment variable.</summary>
    Environment,

    /// <summary><c>appsettings.json</c> or <c>appsettings.{environment}.json</c>.</summary>
    AppSettings,

    /// <summary>The built-in default.</summary>
    Default
}

/// <summary>
/// A configured value and the source that supplied it.
/// </summary>
/// <param name="Value">The value, never blank.</param>
/// <param name="Source">Where it came from.</param>
/// <param name="Origin">
/// The exact place, as a user would name it: <c>--api-key</c>, <c>XPING_APIKEY</c>,
/// <c>appsettings.Development.json</c>, <c>default</c>.
/// </param>
internal sealed record ConfiguredValue(string Value, ConfigurationSource Source, string Origin);

/// <summary>
/// A configuration value that is present but unusable.
/// </summary>
internal sealed class CliConfigurationException : Exception
{
    public CliConfigurationException()
    {
    }

    public CliConfigurationException(string message)
        : base(message)
    {
    }

    public CliConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// The Cloud settings of one CLI invocation: Cloud URL, API key and project id.
/// </summary>
/// <remarks>
/// <para>
/// Precedence, highest first: the command-line option, <c>XPING_*</c>, <c>Xping__*</c>,
/// <c>appsettings.{environment}.json</c>, <c>appsettings.json</c>, the default. The files and
/// variables are the ones the SDK reads, so a project configured for uploads is configured for the
/// CLI too.
/// </para>
/// <para>
/// This is the CLI's own reader rather than the host's <see cref="IConfiguration"/>, which
/// <c>Program.BuildHost</c> deliberately leaves empty: only the commands that talk to Cloud read
/// these files, and only from the directory the report is about.
/// </para>
/// </remarks>
internal sealed class CliConfiguration
{
    private const string Section = "Xping";

    private CliConfiguration(ConfiguredValue cloudUrl, ConfiguredValue? apiKey, ConfiguredValue? projectId)
    {
        CloudUrl = cloudUrl;
        ApiKey = apiKey;
        ProjectId = projectId;
    }

    /// <summary>
    /// Gets the normalized Cloud URL. Always present: the default applies when nothing is set.
    /// </summary>
    public ConfiguredValue CloudUrl { get; }

    /// <summary>
    /// Gets the API key, or <see langword="null"/> when none is configured.
    /// </summary>
    public ConfiguredValue? ApiKey { get; }

    /// <summary>
    /// Gets the project key the SDK is pinned to, or <see langword="null"/>.
    /// </summary>
    public ConfiguredValue? ProjectId { get; }

    /// <summary>
    /// Resolves the configuration.
    /// </summary>
    /// <param name="cloudUrlFlag">The already-normalized <c>--cloud-url</c> value, if given.</param>
    /// <param name="apiKeyFlag">The <c>--api-key</c> value, if given.</param>
    /// <param name="directory">Where <c>appsettings*.json</c> are looked for.</param>
    /// <param name="environment">Reads an environment variable.</param>
    /// <returns>The resolved configuration.</returns>
    /// <exception cref="CliConfigurationException">
    /// A settings file cannot be parsed, or a configured Cloud URL is invalid.
    /// </exception>
    public static CliConfiguration Load(
        string? cloudUrlFlag,
        string? apiKeyFlag,
        string directory,
        Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        IReadOnlyList<(string FileName, IConfiguration Settings)> files = ReadSettingsFiles(directory, environment);

        ConfiguredValue cloudUrl =
            Resolve("CloudUrl", "--cloud-url", cloudUrlFlag, files, environment)
            ?? new ConfiguredValue(Configuration.CloudUrl.Default, ConfigurationSource.Default, "default");

        // The flag was validated by the parser; anything else is validated here, naming the origin so
        // the user knows which of several places holds the bad value.
        if (cloudUrl.Source != ConfigurationSource.Flag)
        {
            if (!Configuration.CloudUrl.TryNormalize(cloudUrl.Value, cloudUrl.Origin, out string? normalized, out string? error))
                throw new CliConfigurationException(error);

            cloudUrl = cloudUrl with { Value = normalized };
        }

        return new CliConfiguration(
            cloudUrl,
            Resolve("ApiKey", "--api-key", apiKeyFlag, files, environment),
            Resolve("ProjectId", "--project", flag: null, files, environment));
    }

    private static ConfiguredValue? Resolve(
        string name,
        string flagName,
        string? flag,
        IReadOnlyList<(string FileName, IConfiguration Settings)> files,
        Func<string, string?> environment)
    {
        if (NonBlank(flag) is { } fromFlag)
            return new ConfiguredValue(fromFlag, ConfigurationSource.Flag, flagName);

        string prefixed = "XPING_" + name.ToUpperInvariant();
        if (NonBlank(environment(prefixed)) is { } fromPrefixed)
            return new ConfiguredValue(fromPrefixed, ConfigurationSource.Environment, prefixed);

        // The SDK reads these through AddEnvironmentVariables, which matches names case-insensitively.
        // A lookup by name cannot enumerate every casing, so it covers the two that are written:
        // the documented one and the all-capitals one shell scripts tend to use.
        foreach (string nested in (string[])[$"{Section}__{name}", $"{Section}__{name}".ToUpperInvariant()])
        {
            if (NonBlank(environment(nested)) is { } fromNested)
                return new ConfiguredValue(fromNested, ConfigurationSource.Environment, nested);
        }

        foreach ((string fileName, IConfiguration settings) in files)
        {
            if (NonBlank(settings[$"{Section}:{name}"]) is { } fromFile)
                return new ConfiguredValue(fromFile, ConfigurationSource.AppSettings, $"{Section}:{name} in {fileName}");
        }

        return null;
    }

    /// <summary>
    /// Reads the settings files, most specific first.
    /// </summary>
    /// <remarks>
    /// Read one by one rather than layered in a single builder so a value can name the file it came
    /// from; layering would answer "what" but not "where", and "where" is what a user fixing a bad
    /// value needs.
    /// </remarks>
    private static IReadOnlyList<(string FileName, IConfiguration Settings)> ReadSettingsFiles(
        string directory, Func<string, string?> environment)
    {
        string environmentName = NonBlank(environment("ASPNETCORE_ENVIRONMENT"))
            ?? NonBlank(environment("DOTNET_ENVIRONMENT"))
            ?? "Production";

        string basePath = Path.GetFullPath(directory);

        // A directory that does not exist holds no settings. The command that named it reports
        // that in its own words; configuration is not the place to fail for it.
        if (!Directory.Exists(basePath))
            return [];

        return
        [
            ($"appsettings.{environmentName}.json", ReadSettingsFile(basePath, $"appsettings.{environmentName}.json")),
            ("appsettings.json", ReadSettingsFile(basePath, "appsettings.json"))
        ];
    }

    private static IConfiguration ReadSettingsFile(string basePath, string fileName)
    {
        try
        {
            return new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile(fileName, optional: true, reloadOnChange: false)
                .Build();
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            throw new CliConfigurationException(
                $"Could not read {Path.Combine(basePath, fileName)}: {ex.Message}", ex);
        }
    }

    private static string? NonBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
