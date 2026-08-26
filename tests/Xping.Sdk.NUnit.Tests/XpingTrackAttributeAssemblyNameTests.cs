/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.NUnit.Tests;

using global::NUnit.Framework.Interfaces;
using global::NUnit.Framework.Internal;
using System;
using System.Reflection;
using Moq;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.Collector;
using Xping.Sdk.Core.Services.Identity;
using Xping.Sdk.Core.Services.Retry;
using Xunit;
using Assert = Xunit.Assert;

/// <summary>
/// Covers which assembly an NUnit execution is attributed to.
/// </summary>
/// <remarks>
/// <para>
/// This matters more than a label. When no project is pinned, the assembly names the Xping Cloud
/// project the execution is filed under, so resolving it wrongly files data in a project nobody
/// meant to create, and failing to resolve it at all used to drop the execution entirely —
/// <c>AfterTest</c> records inside a catch-all, so the exception a blank assembly once raised was
/// swallowed in silence.
/// </para>
/// <para>
/// <see cref="ITest.TypeInfo"/> is null for some synthetic and parameterized wrappers. NUnit will
/// not build one of those from here, so these tests clear the property's backing field to reach the
/// same state and prove the fallback chain covers it.
/// </para>
/// </remarks>
public sealed class XpingTrackAttributeAssemblyNameTests
{
    private static readonly string ThisAssembly =
        typeof(XpingTrackAttributeAssemblyNameTests).Assembly.GetName().Name!;

    [Fact]
    public void AssemblyComesFromTheFixtureTypeWhenNUnitSuppliesOne()
    {
        TestMethod test = TestFor(nameof(SampleTestMethod));

        Assert.NotNull(test.TypeInfo);
        Assert.Equal(ThisAssembly, CapturedAssemblyFor(test));
    }

    [Fact]
    public void AnExecutionIsStillAttributedWhenTypeInfoIsMissing()
    {
        // The regression this guards: with no fallback the assembly was empty, TestIdentityGenerator
        // threw, and AfterTest swallowed it — the test ran and was never reported.
        TestMethod test = TestFor(nameof(SampleTestMethod));
        ClearTypeInfo(test);

        Assert.Null(test.TypeInfo);
        Assert.Equal(ThisAssembly, CapturedAssemblyFor(test));
    }

    [Fact]
    public void TheFallbackPrefersReflectedTypeOverDeclaringType()
    {
        // A fixture inherited from a base class in *another* assembly must be attributed to the test
        // project, not to whatever assembly the base lives in. Both types sit here, so this asserts
        // the ordering that protects that case rather than the cross-assembly outcome itself.
        var fixture = new DerivedFixture();
        MethodInfo inherited = fixture.GetType().GetMethod(
            nameof(BaseFixture.InheritedTestMethod),
            BindingFlags.Instance | BindingFlags.Public)!;

        Assert.Equal(typeof(BaseFixture), inherited.DeclaringType);
        Assert.Equal(typeof(DerivedFixture), inherited.ReflectedType);

        var test = new TestMethod(new MethodWrapper(typeof(DerivedFixture), inherited));
        ClearTypeInfo(test);

        Assert.Equal(ThisAssembly, CapturedAssemblyFor(test));
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    /// <summary>
    /// Runs <c>CreateTestExecution</c> against a stub identity generator and returns the assembly it
    /// was handed.
    /// </summary>
    private static string? CapturedAssemblyFor(ITest test)
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
            .Returns<string, string, object[]?, string?, string?, int?, string?>(
                (_, assembly, _, _, _, _, _) =>
                {
                    captured = assembly;
                    return new TestIdentity { Assembly = assembly };
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
                test,
                DateTime.UtcNow,
                DateTime.UtcNow,
                TimeSpan.FromMilliseconds(1),
                "worker-1",
                typeof(XpingTrackAttributeAssemblyNameTests).FullName
            ]);

        return captured;
    }

    /// <summary>
    /// Puts a test into the state NUnit produces for some synthetic wrappers: a usable
    /// <c>Method</c> handle and no <c>TypeInfo</c>.
    /// </summary>
    private static void ClearTypeInfo(Test test)
    {
        FieldInfo backing = typeof(Test).GetField(
            "<TypeInfo>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        backing.SetValue(test, null);
    }

    private static TestMethod TestFor(string methodName)
    {
        MethodInfo target = typeof(XpingTrackAttributeAssemblyNameTests).GetMethod(
            methodName, BindingFlags.NonPublic | BindingFlags.Static)!;

        return new TestMethod(new MethodWrapper(typeof(XpingTrackAttributeAssemblyNameTests), target));
    }

    private static void SampleTestMethod()
    {
    }
}

/// <summary>A base fixture standing in for one shared from another assembly.</summary>
internal class BaseFixture
{
    private int _calls;

    public void InheritedTestMethod() => _calls++;
}

/// <summary>The concrete fixture NUnit would resolve; inherits its only test method.</summary>
internal sealed class DerivedFixture : BaseFixture
{
}
