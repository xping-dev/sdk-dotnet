/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.NUnit;

using System.Threading.Tasks;
using global::NUnit.Framework;
using Xping.Sdk.Core.Exceptions;

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
        try
        {
            await XpingContext.FinalizeAsync().ConfigureAwait(false);
        }
        catch (XpingNetworkException ex)
        {
            // FailFast aborts the process immediately, which is the correct behavior for strict mode
            // where observability must be guaranteed. Re-throwing from OneTimeTearDown only fails the
            // teardown method and may not cause the CI pipeline to fail with a non-zero exit code.
            Environment.FailFast($"[Xping] Strict mode network error: {ex.Message}", ex);
        }

        await XpingContext.ShutdownAsync().ConfigureAwait(false);
    }
}
