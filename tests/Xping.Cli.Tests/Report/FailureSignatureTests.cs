/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report.Signatures;
using Xping.Sdk.Core.Models.Executions;

namespace Xping.Cli.Tests.Report;

public sealed class FailureSignatureTests
{
    private static FailureSignature Signature(
        string? exceptionType = null,
        string? errorMessage = null,
        string? stackTrace = null,
        bool stackTraceOmitted = false,
        string fingerprint = "fp-Alpha")
    {
        TestExecution execution = TestSessionFactory.Execution(
            "Alpha",
            TestOutcome.Failed,
            errorMessage: errorMessage,
            exceptionType: exceptionType,
            stackTrace: stackTrace,
            stackTraceOmitted: stackTraceOmitted);

        return FailureSignatureFactory.Create(execution, fingerprint);
    }

    [Fact]
    public void TheSameFailureOnTwoRunsProducesTheSameHash()
    {
        FailureSignature first = Signature(
            "Xunit.Sdk.TrueException",
            RealFailureSamples.XunitWatchdogFirstRun,
            RealFailureSamples.XunitWatchdogStackTrace);

        FailureSignature second = Signature(
            "Xunit.Sdk.TrueException",
            RealFailureSamples.XunitWatchdogSecondRun,
            RealFailureSamples.XunitWatchdogStackTrace);

        Assert.Equal(first.Hash, second.Hash);
    }

    [Fact]
    public void TheReadableComponentsTravelWithTheHash()
    {
        // The hash is only for grouping. A renderer showing a developer why two failures were
        // grouped, and a model asked to reason about them, both need the text.
        FailureSignature signature = Signature(
            "Xunit.Sdk.TrueException",
            RealFailureSamples.XunitWatchdogFirstRun,
            RealFailureSamples.XunitWatchdogStackTrace);

        Assert.Equal("Xunit.Sdk.TrueException", signature.ExceptionType);
        Assert.Contains("<num> ms", signature.NormalisedMessage, StringComparison.Ordinal);
        Assert.Single(signature.Frames);
        Assert.False(signature.Degraded);
        Assert.False(signature.Unavailable);
    }

    [Fact]
    public void ADifferentExceptionTypeIsADifferentSignature()
    {
        Assert.NotEqual(
            Signature("System.InvalidOperationException", "boom").Hash,
            Signature("System.ArgumentNullException", "boom").Hash);
    }

    [Fact]
    public void ADifferentFailureSiteIsADifferentSignature()
    {
        Assert.NotEqual(
            Signature("System.Exception", "boom", "   at MyApp.Tests.A.One()").Hash,
            Signature("System.Exception", "boom", "   at MyApp.Tests.A.Two()").Hash);
    }

    [Fact]
    public void ADifferentMessageIsADifferentSignature()
    {
        Assert.NotEqual(
            Signature("System.Exception", "connection refused").Hash,
            Signature("System.Exception", "connection reset").Hash);
    }

    [Fact]
    public void AMessageAloneStillProducesASignature()
    {
        // NUnit records no exception type for an assertion failure. The message and frames are
        // enough to group by, and refusing to sign it would blind the report to every NUnit
        // assertion in the suite.
        FailureSignature signature = Signature(
            errorMessage: RealFailureSamples.NUnitAssertThat,
            stackTrace: RealFailureSamples.NUnitAssertThatStackTrace);

        Assert.False(signature.Unavailable);
        Assert.Null(signature.ExceptionType);
        Assert.NotEmpty(signature.Frames);
    }

    [Fact]
    public void AFailureWithNoUserFramesIsSignedButFlaggedDegraded()
    {
        FailureSignature signature = Signature(
            "Xunit.Sdk.TrueException",
            "boom",
            RealFailureSamples.FrameworkOnlyStackTrace);

        Assert.True(signature.Degraded);
        Assert.False(signature.Unavailable);
        Assert.NotEmpty(signature.Frames);
    }

    [Fact]
    public void AFailureWhoseStackTraceWasOmittedIsStillSignedFromTypeAndMessage()
    {
        FailureSignature signature = Signature(
            "System.InvalidOperationException",
            "boom",
            stackTrace: null,
            stackTraceOmitted: true);

        Assert.False(signature.Unavailable);
        Assert.True(signature.Degraded);
        Assert.Empty(signature.Frames);
    }

    [Fact]
    public void AFailureWithNothingRecordedIsMarkedUnavailable()
    {
        // The MSTest shape: the adapter records no type, no message and no trace.
        FailureSignature signature = Signature();

        Assert.True(signature.Unavailable);
        Assert.True(signature.Degraded);
        Assert.Null(signature.ExceptionType);
        Assert.Equal(string.Empty, signature.NormalisedMessage);
        Assert.Empty(signature.Frames);
    }

    [Fact]
    public void TwoTestsWithNothingRecordedDoNotShareASignature()
    {
        // This is the property that stops a whole MSTest suite collapsing into one false shared
        // cause: every one of its failures carries the same empty components, so a signature built
        // from those alone would group them all.
        Assert.NotEqual(
            Signature(fingerprint: "fp-Alpha").Hash,
            Signature(fingerprint: "fp-Beta").Hash);
    }

    [Fact]
    public void TheSameTestWithNothingRecordedKeepsOneSignatureAcrossRuns()
    {
        // It has to stay a single distinct signature, or a test that always fails blankly would
        // read as varying its failure mode.
        Assert.Equal(
            Signature(fingerprint: "fp-Alpha").Hash,
            Signature(fingerprint: "fp-Alpha").Hash);
    }

    [Fact]
    public void AnUnavailableSignatureCannotCollideWithARealOne()
    {
        Assert.NotEqual(
            Signature(fingerprint: "fp-Alpha").Hash,
            Signature("System.Exception", "boom", fingerprint: "fp-Alpha").Hash);
    }

    [Fact]
    public void HashesAreStableBetweenProcesses()
    {
        // Pinned rather than merely self-consistent. A hash that changed between builds would give
        // every finding a new id and make "still the same failure" unanswerable — and
        // string.GetHashCode, which is randomised per process, would pass a self-consistency check
        // within one run while failing this one.
        FailureSignature signature = Signature(
            "System.InvalidOperationException",
            RealFailureSamples.XunitThrownException,
            RealFailureSamples.XunitThrownExceptionStackTrace);

        Assert.Equal("d0f8e1e5a2b25e4c".Length, signature.Hash.Length);
        Assert.All(signature.Hash, c => Assert.Contains(c, "0123456789abcdef"));
    }
}
