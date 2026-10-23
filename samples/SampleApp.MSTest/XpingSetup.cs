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
    [AssemblyInitialize]
    public static void AssemblyInit(TestContext context)
    {
        XpingContext.Initialize();
    }

    // The integration tests build the sample without it, to reach the SDK's process-exit safety net.
#if !XPING_SKIP_ASSEMBLY_CLEANUP
    [AssemblyCleanup]
    public static async Task AssemblyCleanup()
    {
        await XpingContext.FinalizeAndShutdownAsync().ConfigureAwait(false);
    }
#endif
}
