/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.MSTest;

using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Assembly-level initialization and cleanup for Xping SDK in MSTest projects.
/// Add this class to your test assembly to enable automatic SDK lifecycle management.
/// </summary>
[TestClass]
public static class XpingAssemblyInitialize
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
    /// Finalizes the session by uploading all buffered executions, then disposes SDK resources.
    /// </summary>
    /// <remarks>
    /// MSTest does not reliably invoke this hook under some configurations (e.g. method-level
    /// parallelization — see issue #124). <see cref="XpingContext"/> registers a process-exit safety
    /// net on <see cref="XpingContext.Initialize()"/> that finalizes the session in that case, so this
    /// hook is the primary path, not the only one.
    /// </remarks>
    [AssemblyCleanup]
    public static async Task AssemblyCleanup()
    {
        await XpingContext.FinalizeAndShutdownAsync().ConfigureAwait(false);
    }
}
