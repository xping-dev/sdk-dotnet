/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Hosting;

/// <summary>
/// The options every command accepts, as parsed for this invocation.
/// </summary>
/// <remarks>
/// The host is built before the command line is parsed (the commands are resolved from it), so
/// these are filled in by <c>Program.Run</c> after parsing and before the command runs. Nothing
/// reads them earlier.
/// </remarks>
internal sealed class GlobalOptions
{
    /// <summary>
    /// Gets or sets the normalized <c>--cloud-url</c>, or <see langword="null"/> when not given.
    /// </summary>
    public string? CloudUrl { get; set; }

    /// <summary>
    /// Gets or sets the <c>--api-key</c> value, or <see langword="null"/> when not given.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether <c>--verbose</c> was given.
    /// </summary>
    public bool Verbose { get; set; }
}
