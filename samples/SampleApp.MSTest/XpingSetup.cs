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

    [AssemblyCleanup]
    public static async Task AssemblyCleanup()
    {
        await XpingContext.FlushAsync().ConfigureAwait(false);
        await XpingContext.DisposeAsync().ConfigureAwait(false);
    }
}
