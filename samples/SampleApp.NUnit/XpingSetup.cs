/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.NUnit;

namespace SampleApp.NUnit;

/// <summary>
/// Global setup fixture for Xping SDK initialization.
/// This fixture runs once before any tests execute and ensures
/// that all test executions are properly tracked and uploaded.
/// </summary>
[SetUpFixture]
public class XpingSetup
{
    [OneTimeSetUp]
    public void BeforeAllTests()
    {
        XpingContext.Initialize();
    }

    [OneTimeTearDown]
    public async Task AfterAllTests()
    {
        await XpingContext.FinalizeAsync().ConfigureAwait(false);
        await XpingContext.ShutdownAsync().ConfigureAwait(false);
    }
}
