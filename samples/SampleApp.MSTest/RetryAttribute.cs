/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace SampleApp.MSTest;

/// <summary>
/// Re-runs a failing test method up to <see cref="MaxRetries"/> times, reporting only the last
/// attempt's result to the test platform.
/// </summary>
/// <remarks>
/// <para>
/// MSTest 3.7 ships no retry attribute, so this is the community pattern every MSTest retry helper
/// follows: derive from <see cref="TestMethodAttribute"/> and call <c>base.Execute</c> again for
/// each attempt. It is also the shape MSTest's own <c>[Retry]</c> takes from 3.8 onwards.
/// </para>
/// <para>
/// Each <c>base.Execute</c> call goes through <see cref="ITestMethod.Invoke"/>, which builds a fresh
/// test class instance and runs the full <c>[TestInitialize]</c> → method → <c>[TestCleanup]</c>
/// lifecycle. That is what lets Xping observe every attempt: <c>XpingTestBase</c> records one
/// execution per attempt, numbered in order, so an attempt that failed before a later one passed is
/// still visible in the session even though the build stays green.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RetryAttribute : TestMethodAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RetryAttribute"/> class.
    /// </summary>
    /// <param name="maxRetries">The maximum number of attempts, including the first one.</param>
    public RetryAttribute(int maxRetries) => MaxRetries = maxRetries;

    /// <summary>
    /// Gets the maximum number of attempts, including the first one.
    /// </summary>
    public int MaxRetries { get; }

    /// <inheritdoc/>
    public override TestResult[] Execute(ITestMethod testMethod)
    {
        TestResult[] results = base.Execute(testMethod);

        for (int attempt = 1; attempt < MaxRetries && HasFailed(results); attempt++)
        {
            results = base.Execute(testMethod);
        }

        return results;
    }

    private static bool HasFailed(TestResult[] results) =>
        results.Any(result => result.Outcome == UnitTestOutcome.Failed);
}
