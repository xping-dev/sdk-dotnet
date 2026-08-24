/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report;
using Xping.Cli.Report.Signatures;

namespace Xping.Cli.Tests.Report;

public sealed class StackFrameExtractorTests
{
    [Fact]
    public void FrameworkFramesAreDroppedAndTheCodeUnderTestIsKept()
    {
        FrameExtraction extraction =
            StackFrameExtractor.Extract(RealFailureSamples.XunitWatchdogStackTrace);

        Assert.Equal(
            [
                "SampleApp.XUnit.SampleTests.FlakyTest_EnvironmentState_FailsBasedOnSystemState()"
            ],
            extraction.Frames);

        Assert.False(extraction.Degraded);
    }

    [Fact]
    public void FileNamesAndLineNumbersAreDiscarded()
    {
        FrameExtraction extraction =
            StackFrameExtractor.Extract(RealFailureSamples.XunitThrownExceptionStackTrace);

        // Kept, they would fragment one recurring failure into a new signature per commit that
        // touched anything above it in the file.
        Assert.DoesNotContain(extraction.Frames, f => f.Contains(":line", StringComparison.Ordinal));
        Assert.DoesNotContain(extraction.Frames, f => f.Contains("SampleTests.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void ContinuationMarkersAreNotMistakenForFrames()
    {
        // A real trace carries "--- End of stack trace from previous location ---" between the
        // halves of an awaited call.
        FrameExtraction extraction =
            StackFrameExtractor.Extract(RealFailureSamples.XunitWatchdogStackTrace);

        Assert.DoesNotContain(extraction.Frames, f => f.StartsWith("---", StringComparison.Ordinal));
    }

    [Fact]
    public void NUnitInternalsAreTreatedAsFrameworkFrames()
    {
        FrameExtraction extraction =
            StackFrameExtractor.Extract(RealFailureSamples.NUnitAssertThatStackTrace);

        Assert.Equal(
            [
                "SampleApp.NUnit.SampleTests.FlakyTest_RandomFailure_FailsProbabilistically()"
            ],
            extraction.Frames);
    }

    [Fact]
    public void ProductionCodeFramesAreKeptAlongsideTheTestsOwn()
    {
        // The whole reason the rule is a deny-list. The frame that matters most is usually in the
        // code under test, whose assembly name analysis has no way of knowing.
        FrameExtraction extraction = StackFrameExtractor.Extract(
            "   at MyApp.Services.OrderService.Place(Order order) in /src/OrderService.cs:line 88\n" +
            "   at MyApp.Tests.OrderTests.PlacingAnOrderSucceeds() in /src/OrderTests.cs:line 12\n" +
            "   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)");

        Assert.Equal(
            [
                "MyApp.Services.OrderService.Place(Order order)",
                "MyApp.Tests.OrderTests.PlacingAnOrderSucceeds()"
            ],
            extraction.Frames);
    }

    [Fact]
    public void NoMoreThanTheConfiguredNumberOfFramesIsTaken()
    {
        string trace = string.Join(
            '\n',
            Enumerable.Range(0, 12).Select(i => $"   at MyApp.Deep.Level{i}.Call()"));

        FrameExtraction extraction = StackFrameExtractor.Extract(trace);

        Assert.Equal(LocalAnalysisConstants.SignatureFrameCount, extraction.Frames.Count);
        Assert.Equal("MyApp.Deep.Level0.Call()", extraction.Frames[0]);
    }

    [Fact]
    public void ATraceWithNoUserFramesFallsBackAndIsFlaggedDegraded()
    {
        FrameExtraction extraction =
            StackFrameExtractor.Extract(RealFailureSamples.FrameworkOnlyStackTrace);

        Assert.True(extraction.Degraded);
        Assert.Equal(2, extraction.Frames.Count);
        Assert.StartsWith("Xunit.Assert.True", extraction.Frames[0], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no frames here, just prose")]
    public void ATraceWithNothingToExtractIsEmptyAndDegraded(string? trace)
    {
        FrameExtraction extraction = StackFrameExtractor.Extract(trace);

        Assert.Empty(extraction.Frames);
        Assert.True(extraction.Degraded);
    }

    /// <summary>
    /// The CLR invokes a method interpreted the first time and through an emitted
    /// <c>InvokeStub_{Type}</c> afterwards, so two runs of one failing method produce traces that
    /// differ by that frame alone. Both traces below are real, captured from consecutive tests in a
    /// single NUnit run whose shared <c>[SetUp]</c> threw. Left in, the extra frame gave them
    /// different signatures and stopped three tests blocked by one broken fixture from grouping at
    /// all.
    /// </summary>
    [Fact]
    public void AReflectionInvokeStubDoesNotChangeTheFramesOfAnOtherwiseIdenticalTrace()
    {
        const string interpreted =
            "   at SampleApp.NUnit.BrokenFixture.Setup() in /src/SampleTests.cs:line 180\n" +
            "   at System.Reflection.MethodBaseInvoker.InterpretedInvoke_Method(Object obj, IntPtr* args)\n" +
            "   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)";

        const string stubbed =
            "   at SampleApp.NUnit.BrokenFixture.Setup() in /src/SampleTests.cs:line 180\n" +
            "   at InvokeStub_BrokenFixture.Setup(Object, Object, IntPtr*)\n" +
            "   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)";

        FrameExtraction first = StackFrameExtractor.Extract(interpreted);
        FrameExtraction second = StackFrameExtractor.Extract(stubbed);

        Assert.Equal(["SampleApp.NUnit.BrokenFixture.Setup()"], first.Frames);
        Assert.Equal(first.Frames, second.Frames);
        Assert.False(second.Degraded);
    }

    [Fact]
    public void CarriageReturnsDoNotSurviveIntoAFrame()
    {
        FrameExtraction extraction = StackFrameExtractor.Extract(
            "   at MyApp.Tests.OrderTests.Placing()\r\n   at MyApp.Tests.OrderTests.Other()\r\n");

        Assert.Equal(
            ["MyApp.Tests.OrderTests.Placing()", "MyApp.Tests.OrderTests.Other()"],
            extraction.Frames);
    }
}
