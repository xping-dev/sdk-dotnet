/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Reflection;

namespace Xping.Sdk.Core;

/// <summary>
/// Provides the current version of the Xping SDK, read from assembly metadata at runtime.
/// </summary>
/// <remarks>
/// The version is sourced from <see cref="AssemblyInformationalVersionAttribute"/>, which is
/// stamped onto the assembly during build via the MSBuild <c>Version</c> property
/// (e.g. <c>-p:Version=1.2.3</c> passed by the release workflow).
/// It follows Semantic Versioning 2.0 (e.g. <c>1.2.3</c> or <c>1.3.0-beta.1</c>).
/// Any build metadata suffix (e.g. <c>+abc123</c>) is stripped so that the value is a
/// clean SemVer string suitable for HTTP headers and payload fields.
/// </remarks>
/// <example>
/// <code>
/// // Include in HTTP payloads or headers:
/// string version = XpingSdkVersion.Current; // e.g. "1.2.3" or "1.3.0-beta.1"
/// </code>
/// </example>
public static class XpingSdkVersion
{
    /// <summary>
    /// The current version of the Xping SDK (e.g. <c>"1.2.3"</c> or <c>"1.3.0-beta.1"</c>).
    /// </summary>
    public static readonly string Current = ReadVersion();

    private static string ReadVersion()
    {
        var attribute = typeof(XpingSdkVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>();

        if (attribute == null)
            return "0.0.0";

        string version = attribute.InformationalVersion;
        if (version.Length == 0)
            return "0.0.0";

        // Strip any build metadata suffix (e.g. "1.2.3+abc123def" → "1.2.3")
        int plusIndex = version.IndexOf('+');
        return plusIndex >= 0 ? version.Substring(0, plusIndex) : version;
    }
}
