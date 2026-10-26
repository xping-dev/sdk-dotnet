/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Services.LocalStore;

namespace Xping.Sdk.XUnit.V3;

/// <summary>
/// Tells which test runner started an xUnit v3 test process.
/// </summary>
/// <remarks>
/// <para>
/// An xUnit v3 test assembly is an executable whose generated entry point picks its runner per launch:
/// Microsoft.Testing.Platform when <c>dotnet test</c> or an IDE starts it as a test server (or always,
/// with <c>UseMicrosoftTestingPlatformRunner</c>), and otherwise xUnit's own console runner. vstest
/// runs it through <c>xunit.runner.visualstudio</c>, which launches the executable in that console
/// runner's automated mode. So the entry assembly is the test assembly every time, and its references
/// say nothing about the runner; the call stack does.
/// </para>
/// <para>
/// A run in automated mode is only called vstest when the process also carries vstest's environment,
/// because xUnit's own console and MSBuild runners use that mode too and take different arguments.
/// Anything else stays unknown: a wrong runner prints a command that fails, a missing one none.
/// </para>
/// </remarks>
internal static class TestRunnerDetection
{
    private const string TestingPlatformNamespace = "Xunit.MicrosoftTestingPlatform.";

    /// <summary>
    /// Identifies the runner.
    /// </summary>
    /// <param name="callerTypeNames">The declaring types of the frames on the stack that created the framework.</param>
    /// <param name="commandLineArgs">The process's command line arguments.</param>
    /// <param name="environmentVariableNames">The names of the process's environment variables.</param>
    /// <returns>
    /// <see cref="LocalSessionProperties.TestingPlatformRunner"/>, <see cref="LocalSessionProperties.VSTestRunner"/>,
    /// or <see langword="null"/>.
    /// </returns>
    internal static string? Detect(
        IEnumerable<string?> callerTypeNames,
        IEnumerable<string> commandLineArgs,
        IEnumerable<string?> environmentVariableNames)
    {
        if (callerTypeNames.Any(name => name?.StartsWith(TestingPlatformNamespace, StringComparison.Ordinal) == true))
            return LocalSessionProperties.TestingPlatformRunner;

        // A meta-runner passes its arguments as "-automated …", or in a response file after "@@" once
        // they no longer fit on a command line, which a vstest run with a selection always does.
        bool automated = commandLineArgs.Any(arg =>
            string.Equals(arg, "-automated", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(arg, "@@", StringComparison.Ordinal));

        bool underVSTest = environmentVariableNames.Any(name =>
            name?.StartsWith("VSTEST_", StringComparison.OrdinalIgnoreCase) == true);

        return automated && underVSTest ? LocalSessionProperties.VSTestRunner : null;
    }
}
