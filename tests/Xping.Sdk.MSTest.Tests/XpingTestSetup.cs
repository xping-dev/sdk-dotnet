/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Xping.Sdk.MSTest.Tests;

/// <summary>
/// Assembly-level initialization and cleanup for Xping SDK self-hosted testing.
/// This class initializes Xping context before any tests run and flushes results after all tests complete.
/// </summary>
/// <remarks>
/// This enables the MSTest adapter tests to upload their own test execution results to Xping Cloud,
/// demonstrating the SDK's capabilities and providing real-world validation.
/// </remarks>
[TestClass]
[SuppressMessage("Maintainability", "CA1515:Consider making public types internal")]
public static class XpingTestSetup
{
    [AssemblyInitialize]
    public static void AssemblyInit(TestContext context)
    {
        // Initialize Xping context - will load configuration from appsettings.json and environment variables
        XpingContext.Initialize();
    }

    [AssemblyCleanup]
    public static async Task AssemblyCleanup()
    {
        await XpingContext.FinalizeAsync().ConfigureAwait(false);
        await XpingContext.ShutdownAsync().ConfigureAwait(false);
    }
}
