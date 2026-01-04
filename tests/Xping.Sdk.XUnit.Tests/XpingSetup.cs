/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Diagnostics.CodeAnalysis;

namespace Xping.Sdk.XUnit.Tests;

using NUnit;

/// <summary>
/// Global setup fixture for Xping SDK initialization.
/// This fixture runs once before any tests execute and ensures
/// that all test executions are properly tracked and uploaded.
/// </summary>
[SetUpFixture]
[SuppressMessage("Maintainability", "CA1515:Consider making public types internal")]
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
        await XpingContext.FlushAsync();
        await XpingContext.DisposeAsync();
    }
}
