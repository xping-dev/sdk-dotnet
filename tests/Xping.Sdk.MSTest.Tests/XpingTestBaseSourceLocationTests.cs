/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.MSTest.Tests;

using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.Collector;
using Xping.Sdk.Core.Services.Diagnostics;
using Xping.Sdk.Core.Services.Identity;
using Xping.Sdk.Core.Services.Retry;
using Xunit;
using Assert = Xunit.Assert;

/// <summary>
/// Covers where an MSTest execution is said to be declared.
/// </summary>
/// <remarks>
/// MSTest's <see cref="TestContext"/> exposes no source information at all, so the location comes
/// entirely from the assembly's debug symbols, keyed by the <see cref="MethodInfo"/> the adapter
/// already resolves for the fingerprint and the timeout budget. These tests drive the real
/// <c>CreateTestExecution</c> so the wiring is covered, not just the lookup.
/// </remarks>
public sealed class XpingTestBaseSourceLocationTests
{
    private const string ThisFile = "XpingTestBaseSourceLocationTests.cs";

    [Fact]
    public void ARecordedExecutionCarriesTheDeclarationSiteOfItsTestMethod()
    {
        TestIdentity identity = Identify(
            typeof(SampleTestClass).FullName!, nameof(SampleTestClass.SampleTestMethod));

        Assert.NotNull(identity.SourceFile);
        Assert.EndsWith(ThisFile, identity.SourceFile, StringComparison.Ordinal);
        Assert.Equal(LocationOf<SampleTestClass>(nameof(SampleTestClass.SampleTestMethod)).Line,
            identity.SourceLineNumber);
    }

    /// <summary>
    /// An async test is the shape that would otherwise be lost: its body lives in a compiler-built
    /// state machine, and the method the author wrote keeps only hidden sequence points.
    /// </summary>
    [Fact]
    public void AnAsyncTestMethodIsLocatedAtItsOwnBody()
    {
        TestIdentity identity = Identify(
            typeof(SampleTestClass).FullName!, nameof(SampleTestClass.AsyncTestMethod));

        Assert.NotNull(identity.SourceLineNumber);
        Assert.Equal(LocationOf<SampleTestClass>(nameof(SampleTestClass.AsyncTestMethod)).Line,
            identity.SourceLineNumber);
    }

    /// <summary>
    /// A test method inherited from a base fixture is located where it is written, not where it is
    /// used — the same <c>ReflectedType</c>/<c>DeclaringType</c> split the assembly name has to
    /// navigate, resolved the other way round because a PDB records where code was compiled.
    /// </summary>
    [Fact]
    public void AnInheritedTestMethodIsLocatedInTheBaseFixtureThatDeclaresIt()
    {
        TestIdentity identity = Identify(
            typeof(DerivedTestClass).FullName!, nameof(BaseTestClass.InheritedTestMethod));

        Assert.Equal(LocationOf<BaseTestClass>(nameof(BaseTestClass.InheritedTestMethod)).Line,
            identity.SourceLineNumber);
    }

    /// <summary>
    /// A class the adapter cannot resolve leaves no method to look up. The execution must still be
    /// recorded — a missing location is not a reason to lose the run.
    /// </summary>
    [Fact]
    public void AnUnresolvableTestClassRecordsTheExecutionWithoutALocation()
    {
        TestIdentity identity = Identify("NonExistent.Namespace.Class", "SomeMethod");

        Assert.Null(identity.SourceFile);
        Assert.Null(identity.SourceLineNumber);
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static (string? File, int? Line) LocationOf<T>(string method) =>
        SourceLocationLookup.Of(typeof(T).GetMethod(method)!);

    /// <summary>
    /// Runs the adapter's own execution builder and returns the identity it asked for.
    /// </summary>
    private static TestIdentity Identify(string fullClassName, string testName)
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
            typeof(XpingBaseServices),
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args:
            [
                executionTracker.Object,
                new Mock<IRetryDetector<TestContext>>().Object,
                identityGenerator.Object,
                true
            ],
            culture: null)!;

        MethodInfo create = typeof(XpingTestBase).GetMethod(
            "CreateTestExecution", BindingFlags.NonPublic | BindingFlags.Static)!;

        create.Invoke(
            null,
            [
                services,
                new MockTestContext(testName, fullClassName),
                DateTime.UtcNow,
                DateTime.UtcNow,
                TimeSpan.FromMilliseconds(1),
                "thread-1",
                fullClassName
            ]);

        Assert.NotNull(captured);
        return captured;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "Class is resolved and inspected via reflection, not instantiated directly")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "MicrosoftCodeAnalysisCorrectness",
        "MSTEST0003:Test method signature is invalid",
        Justification = "Test helper class used via reflection, not run by the MSTest runner")]
    private sealed class SampleTestClass
    {
        [TestMethod]
        public void SampleTestMethod()
        {
        }

        [TestMethod]
        public async Task AsyncTestMethod()
        {
            await Task.Yield();
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "Class is resolved and inspected via reflection, not instantiated directly")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "MicrosoftCodeAnalysisCorrectness",
        "MSTEST0003:Test method signature is invalid",
        Justification = "Test helper class used via reflection, not run by the MSTest runner")]
    private class BaseTestClass
    {
        [TestMethod]
        public void InheritedTestMethod()
        {
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "Class is resolved and inspected via reflection, not instantiated directly")]
    private sealed class DerivedTestClass : BaseTestClass
    {
    }

    private sealed class MockTestContext(string testName, string? fullClassName) : TestContext
    {
#pragma warning disable CS8609 // Nullability of reference types in return type doesn't match overridden member
        public override System.Collections.IDictionary Properties { get; } = new Dictionary<string, object?>();
#pragma warning restore CS8609

        public override string TestName => testName;

        public override string? FullyQualifiedTestClassName => fullClassName;

        public override UnitTestOutcome CurrentTestOutcome => UnitTestOutcome.Passed;

        public override void AddResultFile(string fileName) { }

        public override void WriteLine(string? message) { }

        public override void WriteLine(string format, params object?[]? args) { }

        public override void Write(string? message) { }

        public override void Write(string format, params object?[]? args) { }
    }
}
