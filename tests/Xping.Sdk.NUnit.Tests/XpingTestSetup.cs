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
        // Flush any pending test executions to Xping Cloud
        await XpingContext.FlushAsync();

        // Dispose of Xping resources
        await XpingContext.DisposeAsync();
    }
}
