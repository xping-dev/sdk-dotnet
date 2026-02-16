/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.NUnit;

using System.Threading.Tasks;
using global::NUnit.Framework;

/// <summary>
/// Global setup fixture for initializing and disposing Xping SDK in NUnit tests.
/// Place this in the root namespace of your test assembly.
/// </summary>
[SetUpFixture]
public class XpingSetupFixture
{
    /// <summary>
    /// Called once before any tests in the assembly are executed.
    /// Initializes the Xping context.
    /// </summary>
    [OneTimeSetUp]
    public void BeforeAllTests()
    {
        XpingContext.Initialize();
    }

    /// <summary>
    /// Called once after all tests in the assembly have been executed.
    /// Flushes pending test executions and disposes the Xping context.
    /// </summary>
    [OneTimeTearDown]
    public async Task AfterAllTests()
    {
        await XpingContext.FinalizeAsync().ConfigureAwait(false);
        await XpingContext.ShutdownAsync().ConfigureAwait(false);
    }
}
