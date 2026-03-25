/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Executions;

namespace Xping.Sdk.Core.Tests.Models;

public sealed class TestSessionTests
{
    // ---------------------------------------------------------------------------
    // SdkVersion — parameterless constructor (JSON deserialization path)
    // ---------------------------------------------------------------------------

    [Fact]
    public void Constructor_DefaultParameterless_SdkVersionMatchesXpingSdkVersionCurrent()
    {
        var session = new TestSession();

        Assert.Equal(XpingSdkVersion.Current, session.SdkVersion);
    }

    // ---------------------------------------------------------------------------
    // SdkVersion — builder-constructed session (normal creation path)
    // ---------------------------------------------------------------------------

    [Fact]
    public void Build_WithExecution_SdkVersionMatchesXpingSdkVersionCurrent()
    {
        var session = new TestSessionBuilder()
            .AddExecution(
                new TestExecutionBuilder()
                    .WithTestName("SampleTest")
                    .WithOutcome(TestOutcome.Passed)
                    .Build())
            .Build();

        Assert.Equal(XpingSdkVersion.Current, session.SdkVersion);
    }
}
