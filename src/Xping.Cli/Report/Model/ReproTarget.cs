/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Services.LocalStore;

namespace Xping.Cli.Report.Model;

/// <summary>
/// What a repro command runs on: the test runner and the project, as the SDK recorded them.
/// </summary>
/// <param name="Runner">
/// <see cref="LocalSessionProperties.VSTestRunner"/> or <see cref="LocalSessionProperties.TestingPlatformRunner"/>,
/// or null when the SDK could not tell.
/// </param>
/// <param name="ProjectPath">The test project, relative to the repository root, or null when none was stamped.</param>
internal sealed record ReproTarget(string? Runner, string? ProjectPath)
{
    /// <summary>
    /// Gets the target of a window with no session to read it from.
    /// </summary>
    public static ReproTarget Unknown { get; } = new(null, null);

    /// <summary>
    /// Reads the target from a session's recorded properties.
    /// </summary>
    /// <param name="newest">The newest session in the window, or null for an empty one.</param>
    /// <returns>The target.</returns>
    /// <remarks>
    /// <para>
    /// The newest session, because it describes the project as it is now: an older one may predate
    /// a switch of runner, and a command for the old runner fails.
    /// </para>
    /// <para>
    /// One session serves every finding because the window holds one test assembly
    /// (<see cref="Windowing.WindowResolver"/> scopes it), so its newest session ran the same project
    /// each finding's test is in.
    /// </para>
    /// </remarks>
    public static ReproTarget From(TestSession? newest)
    {
        IReadOnlyDictionary<string, string>? properties = newest?.EnvironmentInfo?.CustomProperties;
        if (properties == null)
            return Unknown;

        return new ReproTarget(
            NonEmpty(properties, LocalSessionProperties.Runner),
            NonEmpty(properties, LocalSessionProperties.ProjectPath));
    }

    private static string? NonEmpty(IReadOnlyDictionary<string, string> properties, string key) =>
        properties.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value) ? value : null;
}
