/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace SampleApp.MSTest;

using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xping.Sdk.MSTest;

/// <summary>
/// Assembly-level initialization and cleanup for Xping SDK.
/// This class ensures proper SDK lifecycle management for all tests in the assembly.
/// </summary>
[TestClass]
public static class XpingSetup
{
    /// <summary>
    /// Called once before any tests in the assembly run.
    /// Initializes the Xping SDK context.
    /// </summary>
    /// <param name="context">The assembly test context.</param>
    [AssemblyInitialize]
#pragma warning disable IDE0060 // Remove unused parameter
    public static void AssemblyInit(TestContext context)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        XpingContext.Initialize();
    }

    /// <summary>
    /// Called once after all tests in the assembly complete.
    /// Flushes pending test executions and disposes SDK resources.
    /// </summary>
    [AssemblyCleanup]
    public static async Task AssemblyCleanup()
    {
        await XpingContext.FlushAsync().ConfigureAwait(false);
        await XpingContext.DisposeAsync().ConfigureAwait(false);
    }
}
