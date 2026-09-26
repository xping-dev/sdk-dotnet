/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Diagnostics.CodeAnalysis;
using NUnit.Framework;
using Xping.Sdk.NUnit;

// Apply XpingTrack to all tests in this assembly
[assembly: XpingTrack]

namespace Xping.Sdk.NUnit.Tests;

/// <summary>
/// Global setup fixture for Xping SDK self-hosted testing.
/// This fixture initializes Xping context before any tests run and flushes results after all tests complete.
/// </summary>
/// <remarks>
/// This enables the NUnit adapter tests to upload their own test execution results to Xping Cloud,
/// demonstrating the SDK's capabilities and providing real-world validation.
/// </remarks>
[SetUpFixture]
[SuppressMessage("Maintainability", "CA1515:Consider making public types internal")]
public class XpingTestSetup
{
    [OneTimeSetUp]
    public void BeforeAllTests()
    {
        // Initialize Xping context - will load configuration from appsettings.json and environment variables
        XpingContext.Initialize();
    }

    [OneTimeTearDown]
    public async Task AfterAllTests()
    {
        // Not FinalizeAndShutdownAsync(), which user fixtures should call: it fails the process fast on a
        // strict-mode network error. xUnit tests in this project share the static context and install
        // strict-mode contexts aimed at an unreachable endpoint, and this teardown can run while one of
        // them is live (see docs/known-limitations.md). Here that would crash the test host.
        await XpingContext.FinalizeAsync().ConfigureAwait(true);
        await XpingContext.ShutdownAsync().ConfigureAwait(true);
    }
}
