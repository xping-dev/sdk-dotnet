/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Reflection;
using Moq;
using Xping.Sdk.Core.Services.Diagnostics;
using Xunit.Abstractions;

namespace Xping.Sdk.XUnit.Tests;

/// <summary>
/// Covers where a test is said to be declared.
/// </summary>
/// <remarks>
/// xUnit is the only one of the three frameworks that discovers source information itself, and it
/// only does so when the runner asked it to. Both routes are covered here because a report must not
/// spell the same file two ways depending on which one found it.
/// </remarks>
public sealed class XpingMessageSinkSourceLocationTests
{
    private const string ThisFile = "XpingMessageSinkSourceLocationTests.cs";

    /// <summary>
    /// When the runner supplied source information, it is preferred — it is the framework's own
    /// answer and costs nothing to read.
    /// </summary>
    [Fact]
    public void TheRunnersOwnSourceInformationIsUsedWhenItSuppliedAny()
    {
        (string? file, int? line) = XpingMessageSink.ResolveSourceLocation(
            TestCase(fileName: "/build/shop/tests/CartTests.cs", lineNumber: 42),
            TestMethod(nameof(Probe)));

        Assert.Equal("/build/shop/tests/CartTests.cs", file);
        Assert.Equal(42, line);
    }

    /// <summary>
    /// A deterministic build's path is shortened on the runner's route too, so xUnit's output matches
    /// what the PDB route produces for the same file.
    /// </summary>
    [Fact]
    public void TheRunnersPathGoesThroughTheSameRelativizerAsTheFallback()
    {
        (string? file, _) = XpingMessageSink.ResolveSourceLocation(
            TestCase(fileName: "/_/tests/CartTests.cs", lineNumber: 42),
            TestMethod(nameof(Probe)));

        Assert.Equal("tests/CartTests.cs", file);
    }

    /// <summary>
    /// The case that matters most: a runner that did not enable source discovery leaves
    /// <c>SourceInformation</c> null, and before the PDB fallback existed the location was simply
    /// lost.
    /// </summary>
    [Fact]
    public void NoSourceInformationFallsBackToTheAssemblysDebugSymbols()
    {
        (string? file, int? line) = XpingMessageSink.ResolveSourceLocation(
            TestCase(fileName: null, lineNumber: null),
            TestMethod(nameof(Probe)));

        Assert.NotNull(file);
        Assert.EndsWith(ThisFile, file, StringComparison.Ordinal);
        Assert.Equal(SourceLocationLookup.Of(ProbeMethod()).Line, line);
    }

    /// <summary>
    /// Source information present but empty is the same as absent — xUnit reports it that way for a
    /// test case it could not locate.
    /// </summary>
    [Fact]
    public void EmptySourceInformationFallsBackRatherThanReportingABlankPath()
    {
        (string? file, _) = XpingMessageSink.ResolveSourceLocation(
            TestCase(fileName: string.Empty, lineNumber: null),
            TestMethod(nameof(Probe)));

        Assert.NotNull(file);
        Assert.EndsWith(ThisFile, file, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static ITestCase TestCase(string? fileName, int? lineNumber)
    {
        var testCase = new Mock<ITestCase>();

        if (fileName == null && lineNumber == null)
        {
            testCase.SetupGet(c => c.SourceInformation).Returns((ISourceInformation)null!);
            return testCase.Object;
        }

        var source = new Mock<ISourceInformation>();
        source.SetupGet(s => s.FileName).Returns(fileName!);
        source.SetupGet(s => s.LineNumber).Returns(lineNumber);
        testCase.SetupGet(c => c.SourceInformation).Returns(source.Object);

        return testCase.Object;
    }

    /// <summary>
    /// An <see cref="ITestMethod"/> naming a method in this assembly, so the fallback has real
    /// debug symbols to find.
    /// </summary>
    private static ITestMethod TestMethod(string name)
    {
        var type = new Mock<ITypeInfo>();
        type.SetupGet(t => t.Name).Returns(typeof(XpingMessageSinkSourceLocationTests).FullName!);

        var method = new Mock<IMethodInfo>();
        method.SetupGet(m => m.Name).Returns(name);
        method.SetupGet(m => m.Type).Returns(type.Object);

        var testMethod = new Mock<ITestMethod>();
        testMethod.SetupGet(m => m.Method).Returns(method.Object);

        return testMethod.Object;
    }

    private static MethodInfo ProbeMethod() =>
        typeof(XpingMessageSinkSourceLocationTests).GetMethod(
            nameof(Probe), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static void Probe()
    {
    }
}
