/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.NUnit.Tests;

using global::NUnit.Framework.Interfaces;
using global::NUnit.Framework.Internal;
using System;
using System.Reflection;
using System.Threading.Tasks;
using Xping.Sdk.Core.Services.Diagnostics;
using Xunit;
using Assert = Xunit.Assert;

/// <summary>
/// Covers where an NUnit test is said to be declared.
/// </summary>
/// <remarks>
/// <para>
/// NUnit's test model carries no source information — <see cref="ITest"/> exposes a name, a fixture
/// and properties, and nothing about a file. What it does expose is <c>ITest.Method.MethodInfo</c>,
/// which is enough to reach the assembly's debug symbols, so these tests drive NUnit's own test
/// model rather than a stand-in: if a future NUnit stops populating that handle, they fail.
/// </para>
/// <para>
/// The full <c>CreateTestExecution</c> cannot be driven from here — it reads
/// <c>TestContext.CurrentContext.Result</c>, which only exists inside an NUnit run. The sample
/// projects cover that end to end.
/// </para>
/// </remarks>
public sealed class XpingTrackAttributeSourceLocationTests
{
    private const string ThisFile = "XpingTrackAttributeSourceLocationTests.cs";

    [Fact]
    public void ATestNUnitBuiltCarriesAMethodHandleTheLookupCanLocate()
    {
        TestMethod test = TestFor(nameof(SampleTestMethod));

        (string? file, int? line) = SourceLocationLookup.Of(test.Method?.MethodInfo);

        Assert.NotNull(file);
        Assert.EndsWith(ThisFile, file, StringComparison.Ordinal);
        Assert.NotNull(line);
    }

    /// <summary>
    /// An async test is the shape that would otherwise be lost, and NUnit fixtures are full of them.
    /// </summary>
    [Fact]
    public void AnAsyncTestIsLocatedAtItsOwnBodyRatherThanItsStateMachine()
    {
        TestMethod test = TestFor(nameof(AsyncSampleTestMethod));

        (string? file, int? line) = SourceLocationLookup.Of(test.Method?.MethodInfo);

        Assert.NotNull(file);
        Assert.EndsWith(ThisFile, file, StringComparison.Ordinal);
        Assert.NotNull(line);
    }

    /// <summary>
    /// Each test resolves to its own line, which is what stops a report from pointing every finding
    /// in a fixture at the same place.
    /// </summary>
    [Fact]
    public void TwoTestsInOneFixtureResolveToDifferentLines()
    {
        int? first = SourceLocationLookup.Of(TestFor(nameof(SampleTestMethod)).Method?.MethodInfo).Line;
        int? second = SourceLocationLookup.Of(TestFor(nameof(AsyncSampleTestMethod)).Method?.MethodInfo).Line;

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotEqual(first, second);
    }

    /// <summary>
    /// NUnit hands the adapter a null method for a suite rather than a test case, and the adapter
    /// passes whatever it gets straight through.
    /// </summary>
    [Fact]
    public void ATestWithNoMethodHandleResolvesToNothing()
    {
        (string? file, int? line) = SourceLocationLookup.Of(null);

        Assert.Null(file);
        Assert.Null(line);
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    // Returns the concrete type NUnit builds; the adapter sees it as ITest, and it is ITest.Method
    // that these tests are really about.
    private static TestMethod TestFor(string methodName)
    {
        MethodInfo target = typeof(XpingTrackAttributeSourceLocationTests).GetMethod(
            methodName, BindingFlags.NonPublic | BindingFlags.Static)!;

        return new TestMethod(new MethodWrapper(typeof(XpingTrackAttributeSourceLocationTests), target));
    }

    private static void SampleTestMethod()
    {
    }

    private static async Task AsyncSampleTestMethod()
    {
        await Task.Yield();
    }
}
