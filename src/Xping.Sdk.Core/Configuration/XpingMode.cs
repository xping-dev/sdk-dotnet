/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Configuration;

/// <summary>
/// Determines whether the SDK collects test data, uploads it to the Xping platform, or both.
/// </summary>
/// <remarks>
/// The configured value is resolved to a concrete mode by
/// <see cref="XpingConfiguration.ResolveMode"/>. Only <see cref="Auto"/> is resolved;
/// every other value is honored as specified.
/// </remarks>
public enum XpingMode
{
    /// <summary>
    /// Resolve the mode from the rest of the configuration: <see cref="Connected"/> when
    /// credentials are present or strict mode is enabled, otherwise <see cref="LocalOnly"/>.
    /// This is the default.
    /// </summary>
    Auto = 0,

    /// <summary>
    /// Collect test executions and write them to the local store without contacting the
    /// Xping platform. No API key is required and no network calls are made.
    /// </summary>
    LocalOnly = 1,

    /// <summary>
    /// Collect test executions and upload them to the Xping platform.
    /// Requires a valid API key and project ID.
    /// </summary>
    Connected = 2,

    /// <summary>
    /// Disable the SDK entirely. No collection, no local storage, and no uploads.
    /// Equivalent to setting <see cref="XpingConfiguration.Enabled"/> to <see langword="false"/>.
    /// </summary>
    Disabled = 3
}
