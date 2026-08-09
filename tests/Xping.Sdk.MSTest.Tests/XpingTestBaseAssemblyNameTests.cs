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
using Xping.Sdk.Core.Services.Identity;
using Xping.Sdk.Core.Services.Retry;
using Xunit;
using Assert = Xunit.Assert;

/// <summary>
/// Regression tests for issue #109: TestIdentity.Assembly must be the real assembly's simple
/// name, not the first segment of the test class's namespace (which only coincides for
/// single-segment namespaces and is wrong whenever a namespace has more than one segment).
/// </summary>
public sealed class XpingTestBaseAssemblyNameTests
{
    [Fact]
    public void CreateTestExecution_ResolvesAssemblyFromTestMethodType_NotNamespaceRoot()
    {
        string fullClassName = typeof(SampleTestClass).FullName!;
        string expectedAssembly = typeof(SampleTestClass).Assembly.GetName().Name!;

        // The namespace root ("Xping") differs from the real assembly's simple name
        // ("Xping.Sdk.MSTest.Tests") — exactly the mismatch issue #109 reported.
        Assert.NotEqual(fullClassName.Split('.')[0], expectedAssembly);

        var context = new MockTestContext(nameof(SampleTestClass.SampleTestMethod), fullClassName);

        TestExecution execution = InvokeCreateTestExecution(context, out string? capturedAssembly);

        Assert.Equal(expectedAssembly, capturedAssembly);
        Assert.Equal(expectedAssembly, execution.Identity.Assembly);
    }

    [Fact]
    public void CreateTestExecution_UnresolvableTestClass_FallsBackToNamespaceHeuristic()
    {
        var context = new MockTestContext("SomeMethod", "NonExistent.Namespace.Class");

        InvokeCreateTestExecution(context, out string? capturedAssembly);

        Assert.Equal("NonExistent", capturedAssembly);
    }

    [Fact]
    public void ExtractAssemblyName_WithMultiSegmentNamespace_ReturnsFirstSegmentAsFallback()
    {
        MethodInfo method = typeof(XpingTestBase).GetMethod(
            "ExtractAssemblyName",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        var result = (string?)method.Invoke(null, ["MyCompany.Billing.Tests.InvoiceTests"]);

        Assert.Equal("MyCompany", result);
    }

    private static TestExecution InvokeCreateTestExecution(TestContext context, out string? capturedAssembly)
    {
        string? captured = null;
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
            .Callback<string, string, object[]?, string?, string?, int?, string?>(
                (_, assembly, _, _, _, _, _) => captured = assembly)
            .Returns<string, string, object[]?, string?, string?, int?, string?>(
                (_, assembly, _, _, _, _, _) => new TestIdentity { Assembly = assembly });

        var executionTracker = new Mock<IExecutionTracker>();
        executionTracker
            .Setup(t => t.CreateExecutionContext(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>()))
            .Returns(new TestOrchestrationRecord());

        var retryDetector = new Mock<IRetryDetector<TestContext>>();

        object services = Activator.CreateInstance(
            typeof(XpingBaseServices),
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args: [executionTracker.Object, retryDetector.Object, identityGenerator.Object, true],
            culture: null)!;

        MethodInfo method = typeof(XpingTestBase).GetMethod(
            "CreateTestExecution",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        var execution = (TestExecution)method.Invoke(
            null,
            [
                services,
                context,
                DateTime.UtcNow,
                DateTime.UtcNow,
                TimeSpan.FromMilliseconds(1),
                "thread-1",
                "SampleTestClass"
            ])!;

        capturedAssembly = captured;
        return execution;
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
