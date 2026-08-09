/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit.Abstractions;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.Collector;
using Xping.Sdk.Core.Services.Identity;
using Xping.Sdk.Core.Services.Retry;

namespace Xping.Sdk.XUnit.Tests;

/// <summary>
/// Regression tests for issue #109: TestIdentity.Assembly must be the simple assembly name
/// (e.g. "SampleApp.XUnit"), not xUnit's IAssemblyInfo.Name, which returns the full display
/// name including Version/Culture/PublicKeyToken.
/// </summary>
public sealed class XpingMessageSinkAssemblyNameTests
{
    [Fact]
    public void CreateTestExecution_UsesConstructorSuppliedSimpleAssemblyName_NotAssemblyDisplayName()
    {
        const string simpleAssemblyName = "SampleApp.XUnit";
        const string fullDisplayName = "SampleApp.XUnit, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";

        string? capturedAssembly = null;
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
                (_, assembly, _, _, _, _, _) => capturedAssembly = assembly)
            .Returns(new TestIdentity());

        var executionTracker = new Mock<IExecutionTracker>();
        executionTracker
            .Setup(t => t.CreateExecutionContext(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>()))
            .Returns(new TestOrchestrationRecord());

        var retryDetector = new Mock<IRetryDetector<ITest>>();

        var sink = new XpingMessageSink(
            Mock.Of<IMessageSink>(),
            executionTracker.Object,
            retryDetector.Object,
            identityGenerator.Object,
            NullLogger<XpingMessageSink>.Instance,
            captureStackTraces: true,
            assemblyName: simpleAssemblyName);

        ITest test = BuildFakeTest(fullDisplayName);

        InvokeCreateTestExecution(sink, test);

        Assert.Equal(simpleAssemblyName, capturedAssembly);
    }

    /// <summary>
    /// Builds a fake xUnit <see cref="ITest"/> whose class Assembly.Name returns a
    /// version-qualified display name, so the test fails if CreateTestExecution ever falls back
    /// to deriving the assembly name from the xUnit abstraction instead of the constructor value.
    /// </summary>
    private static ITest BuildFakeTest(string assemblyDisplayName)
    {
        var assemblyInfo = new Mock<IAssemblyInfo>();
        assemblyInfo.SetupGet(a => a.Name).Returns(assemblyDisplayName);

        var classInfo = new Mock<ITypeInfo>();
        classInfo.SetupGet(c => c.Name).Returns("SampleApp.XUnit.CalculatorTests");
        classInfo.SetupGet(c => c.Assembly).Returns(assemblyInfo.Object);

        var methodType = new Mock<ITypeInfo>();
        methodType.SetupGet(t => t.Name).Returns("SampleApp.XUnit.CalculatorTests");

        var methodInfo = new Mock<IMethodInfo>();
        methodInfo.SetupGet(m => m.Name).Returns("Add_ReturnsSum");
        methodInfo.SetupGet(m => m.Type).Returns(methodType.Object);

        var testClass = new Mock<ITestClass>();
        testClass.SetupGet(c => c.Class).Returns(classInfo.Object);

        var testMethod = new Mock<ITestMethod>();
        testMethod.SetupGet(m => m.Method).Returns(methodInfo.Object);
        testMethod.SetupGet(m => m.TestClass).Returns(testClass.Object);

        var testCase = new Mock<ITestCase>();
        testCase.SetupGet(c => c.TestMethod).Returns(testMethod.Object);
        testCase.SetupGet(c => c.DisplayName).Returns("Add_ReturnsSum");

        var test = new Mock<ITest>();
        test.SetupGet(t => t.TestCase).Returns(testCase.Object);
        test.SetupGet(t => t.DisplayName).Returns("Add_ReturnsSum");

        return test.Object;
    }

    private static void InvokeCreateTestExecution(XpingMessageSink sink, ITest test)
    {
        MethodInfo method = typeof(XpingMessageSink).GetMethod(
            "CreateTestExecution",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

        method.Invoke(
            sink,
            [
                test,
                TestOutcome.Passed,
                DateTime.UtcNow,
                DateTime.UtcNow,
                TimeSpan.FromMilliseconds(1),
                string.Empty,
                null,
                null,
                null,
                "worker-1"
            ]);
    }
}
