/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Xping.Sdk.Core.Services.Environment;

namespace Xping.Sdk.Core.Services.PullRequest.Internals;

/// <summary>
/// Environment reading and parsing shared by the platform pull request detectors.
/// </summary>
internal static class PullRequestEnvironment
{
    private const string HeadsPrefix = "refs/heads/";

    /// <summary>
    /// Reads a variable that detection can't continue without, logging which one was missing.
    /// </summary>
    public static bool TryGetRequired(
        IEnvironmentVariableProvider env,
        ILogger logger,
        string platform,
        string variable,
        [NotNullWhen(true)] out string? value)
    {
        string? raw = env.GetVariable(variable);
        if (string.IsNullOrWhiteSpace(raw))
        {
            logger.LogDebug("{Platform} PR detection skipped: {Variable} is not set.", platform, variable);
            value = null;
            return false;
        }

        value = raw!; // IsNullOrWhiteSpace guarantees non-null; ! informs the compiler
        return true;
    }

    /// <summary>
    /// Reads a required variable holding a PR number.
    /// </summary>
    public static bool TryGetRequiredNumber(
        IEnvironmentVariableProvider env,
        ILogger logger,
        string platform,
        string variable,
        out int number)
    {
        number = 0;
        if (!TryGetRequired(env, logger, platform, variable, out string? raw))
            return false;

        if (int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out number) && number > 0)
            return true;

        logger.LogDebug(
            "{Platform} PR detection skipped: {Variable}='{Value}' is not a PR number.",
            platform, variable, raw);
        return false;
    }

    /// <summary>
    /// Splits a repository path into owner and name at the last <c>/</c>, so GitLab's nested
    /// groups stay in the owner (<c>group/sub/project</c> → <c>group/sub</c>, <c>project</c>).
    /// </summary>
    public static bool TrySplitAtLastSlash(
        string path,
        [NotNullWhen(true)] out string? owner,
        [NotNullWhen(true)] out string? name)
    {
        int slashIndex = path.LastIndexOf('/');
        if (slashIndex <= 0 || slashIndex == path.Length - 1)
        {
            owner = null;
            name = null;
            return false;
        }

        owner = path.Substring(0, slashIndex);
        name = path.Substring(slashIndex + 1);
        return true;
    }

    /// <summary>
    /// Normalizes a server URL to scheme, lowercase host, non-default port and path, with no trailing
    /// <c>/</c>. Query, fragment and credentials are dropped. Only absolute http(s) URLs are accepted.
    /// </summary>
    public static bool TryNormalizeServerUrl(string? raw, [NotNullWhen(true)] out string? serverUrl) =>
        TryNormalize(raw, keepPath: true, out serverUrl);

    /// <summary>
    /// Like <see cref="TryNormalizeServerUrl"/>, but keeps only the scheme, host and port of a URL that
    /// points into the server, such as a repository URL.
    /// </summary>
    public static bool TryGetServerRoot(string? raw, [NotNullWhen(true)] out string? serverUrl) =>
        TryNormalize(raw, keepPath: false, out serverUrl);

    private static bool TryNormalize(string? raw, bool keepPath, [NotNullWhen(true)] out string? serverUrl)
    {
        serverUrl = null;
        if (string.IsNullOrWhiteSpace(raw)
            || !Uri.TryCreate(raw!.Trim(), UriKind.Absolute, out Uri? uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || uri.Host.Length == 0)
            return false;

        // Built by hand rather than with GetLeftPart: Azure repository URIs carry the organization as
        // user info (https://org@dev.azure.com/...), which must not become part of the identity.
        // Uri already lowercases the scheme and host.
        string port = uri.IsDefaultPort ? string.Empty : ":" + uri.Port.ToString(CultureInfo.InvariantCulture);
        string path = keepPath ? uri.AbsolutePath.TrimEnd('/') : string.Empty;
        serverUrl = uri.Scheme + "://" + uri.Host + port + path;
        return true;
    }

    /// <summary>
    /// Removes a leading <c>refs/heads/</c>; branch names without it are returned unchanged.
    /// </summary>
    public static string StripHeadsPrefix(string branch) =>
        branch.StartsWith(HeadsPrefix, StringComparison.Ordinal)
            ? branch.Substring(HeadsPrefix.Length)
            : branch;
}
