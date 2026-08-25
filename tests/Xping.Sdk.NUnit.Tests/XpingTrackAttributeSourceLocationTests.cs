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
using Moq;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.Collector;
using Xping.Sdk.Core.Services.Diagnostics;
using Xping.Sdk.Core.Services.Identity;
using Xping.Sdk.Core.Services.Retry;
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
    // Through CreateTestExecution
    // ---------------------------------------------------------------------

    /// <summary>
    /// The wiring, not just the lookup: the location has to survive the journey from
    /// <c>ITest.Method.MethodInfo</c> into the identity the execution carries.
    /// </summary>
    [Fact]
    public void ARecordedExecutionCarriesTheDeclarationSiteOfItsTestMethod()
    {
        TestIdentity identity = Identify(nameof(SampleTestMethod));

        Assert.NotNull(identity.SourceFile);
        Assert.EndsWith(ThisFile, identity.SourceFile, StringComparison.Ordinal);
        AssertBodyStart(identity.SourceLineNumber, nameof(SampleTestMethod));
    }

    [Fact]
    public void AnAsyncTestRecordsTheLineOfItsOwnBody()
    {
        TestIdentity identity = Identify(nameof(AsyncSampleTestMethod));

        Assert.NotNull(identity.SourceFile);
        AssertBodyStart(identity.SourceLineNumber, nameof(AsyncSampleTestMethod));
    }

    /// <summary>
    /// Two tests in one fixture must not collapse onto one line once the identity is built — the
    /// same property the lookup guarantees, checked after the value has been through the adapter.
    /// </summary>
    [Fact]
    public void TwoRecordedExecutionsCarryDifferentLines()
    {
        Assert.NotEqual(
            Identify(nameof(SampleTestMethod)).SourceLineNumber,
            Identify(nameof(AsyncSampleTestMethod)).SourceLineNumber);
    }

    /// <summary>
    /// Runs the adapter's own execution builder and returns the identity it asked for.
    /// </summary>
    private static TestIdentity Identify(string methodName)
    {
        TestIdentity? captured = null;

        var identityGenerator = new Mock<ITestIdentityGenerator>();
        identityGenerator
            .Setup(g => g.Generate(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object[]?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<int?>(),
                It.IsAny<string?>()))
            .Returns<string, string, object[]?, string?, string?, int?, string?>(
                (_, assembly, _, _, sourceFile, sourceLineNumber, _) => captured = new TestIdentity
                {
                    Assembly = assembly,
                    SourceFile = sourceFile,
                    SourceLineNumber = sourceLineNumber
                });

        var executionTracker = new Mock<IExecutionTracker>();
        executionTracker
            .Setup(t => t.CreateExecutionContext(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>()))
            .Returns(new TestOrchestrationRecord());

        object services = Activator.CreateInstance(
            typeof(XpingAttributeServices),
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args:
            [
                executionTracker.Object,
                new Mock<IRetryDetector<ITest>>().Object,
                identityGenerator.Object,
                true
            ],
            culture: null)!;

        MethodInfo create = typeof(XpingTrackAttribute).GetMethod(
            "CreateTestExecution", BindingFlags.NonPublic | BindingFlags.Static)!;

        create.Invoke(
            null,
            [
                services,
                TestFor(methodName),
                DateTime.UtcNow,
                DateTime.UtcNow,
                TimeSpan.FromMilliseconds(1),
                "worker-1",
                typeof(XpingTrackAttributeSourceLocationTests).FullName
            ]);

        Assert.NotNull(captured);
        return captured;
    }

    /// <summary>
    /// Asserts a line is the start of the named probe's body.
    /// </summary>
    /// <remarks>
    /// A debug build reports the opening brace and an optimised build the first statement, so the
    /// value is checked against the lookup's own answer for the same method rather than a constant:
    /// the point here is that the adapter carried it through unchanged, not what the compiler chose.
    /// </remarks>
    private static void AssertBodyStart(int? actual, string methodName)
    {
        Assert.NotNull(actual);
        Assert.Equal(SourceLocationLookup.Of(TestFor(methodName).Method?.MethodInfo).Line, actual);
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
