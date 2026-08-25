/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Executions;

namespace Xping.Sdk.Core.Tests.Models;

public class FailureSiteExtensionsTests
{
    [Theory]
    [InlineData(FailureSite.TestSetup)]
    [InlineData(FailureSite.TestTeardown)]
    [InlineData(FailureSite.FixtureSetup)]
    [InlineData(FailureSite.FixtureTeardown)]
    [InlineData(FailureSite.AssemblySetup)]
    [InlineData(FailureSite.AssemblyTeardown)]
    public void IsLifecycle_SharedLifecycleCode_ReturnsTrue(FailureSite site)
    {
        Assert.True(site.IsLifecycle());
    }

    [Fact]
    public void IsLifecycle_TestBody_ReturnsFalse()
    {
        Assert.False(FailureSite.TestBody.IsLifecycle());
    }

    /// <summary>
    /// An unresolved site is an admission that the adapter could not classify the failure, not an
    /// observation that lifecycle code broke. Counting it as lifecycle would let every failure an
    /// adapter failed to classify feed a finding that names a fixture as the defect.
    /// </summary>
    [Fact]
    public void IsLifecycle_Unknown_ReturnsFalse()
    {
        Assert.False(FailureSite.Unknown.IsLifecycle());
    }

    /// <summary>
    /// Pins the classification of every declared member, so adding a site forces a decision rather
    /// than inheriting one by omission — the same guard <see cref="TestOutcomeExtensionsTests"/> keeps
    /// over <see cref="TestOutcome"/>.
    /// </summary>
    [Fact]
    public void IsLifecycle_EveryDeclaredSite_IsClassifiedDeliberately()
    {
        var expected = new Dictionary<FailureSite, bool>
        {
            [FailureSite.Unknown] = false,
            [FailureSite.TestBody] = false,
            [FailureSite.TestSetup] = true,
            [FailureSite.TestTeardown] = true,
            [FailureSite.FixtureSetup] = true,
            [FailureSite.FixtureTeardown] = true,
            [FailureSite.AssemblySetup] = true,
            [FailureSite.AssemblyTeardown] = true,
        };

        var declared = Enum.GetValues<FailureSite>();

        Assert.Equal(expected.Count, declared.Length);

        foreach (FailureSite site in declared)
        {
            Assert.True(expected.ContainsKey(site), $"FailureSite.{site} has no expected classification.");
            Assert.Equal(expected[site], site.IsLifecycle());
        }
    }

    /// <summary>
    /// Unknown is zero so that a record built without the adapter resolving a site cannot silently
    /// claim the test body failed — a claim about the code under test, made without evidence.
    /// </summary>
    [Fact]
    public void Unknown_IsTheDefaultValue()
    {
        Assert.Equal(FailureSite.Unknown, default(FailureSite));
    }
}
