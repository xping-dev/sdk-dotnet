/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Tests.Report;

/// <summary>
/// The trait category of the Monte Carlo calibration tests: false-positive rates and power, over
/// thousands of simulated suites.
/// </summary>
/// <remarks>
/// They take minutes rather than seconds and only fail when the statistics change, so CI skips
/// them on pull requests and runs them nightly (<c>.github/workflows/nightly.yml</c>).
/// </remarks>
internal static class Calibration
{
    public const string Category = "Calibration";
}
