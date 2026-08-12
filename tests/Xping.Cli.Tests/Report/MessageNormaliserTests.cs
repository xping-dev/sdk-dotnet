/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report.Signatures;

namespace Xping.Cli.Tests.Report;

public sealed class MessageNormaliserTests
{
    [Fact]
    public void TwoRunsOfOneFailureConvergeOnTheSameMessage()
    {
        // The reason normalisation exists. These two strings are the same test failing the same way
        // on two different runs; only the millisecond readings differ. Without this they are two
        // signatures and the failure looks novel every time it happens.
        string first = MessageNormaliser.Normalise(RealFailureSamples.XunitWatchdogFirstRun);
        string second = MessageNormaliser.Normalise(RealFailureSamples.XunitWatchdogSecondRun);

        Assert.Equal(first, second);
        Assert.Contains("watchdog (<num> ms) fired", first, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSurroundingProseSurvivesNormalisation()
    {
        // Only the varying parts go. What is left has to stay specific enough that two genuinely
        // different failures do not collapse into one signature.
        string normalised = MessageNormaliser.Normalise(RealFailureSamples.XunitWatchdogFirstRun);

        Assert.Contains("service-side back-pressure", normalised, StringComparison.Ordinal);
        Assert.Contains("task-scheduling order.", normalised, StringComparison.Ordinal);
    }

    [Fact]
    public void TimestampsAndDurationsInsideARecordDumpAreReplaced()
    {
        string normalised = MessageNormaliser.Normalise(RealFailureSamples.XunitAssertEmpty);

        Assert.DoesNotContain("2026-08-10T20:08:30.8241590Z", normalised, StringComparison.Ordinal);
        Assert.DoesNotContain("00:00:00.0028939", normalised, StringComparison.Ordinal);
        Assert.Contains("endedat = <time>", normalised, StringComparison.Ordinal);
        Assert.Contains("duration = <time>", normalised, StringComparison.Ordinal);
    }

    [Fact]
    public void TheAssertionItselfSurvivesARecordDump()
    {
        string normalised = MessageNormaliser.Normalise(RealFailureSamples.XunitAssertEmpty);

        Assert.StartsWith("assert.empty() failure: collection was not empty", normalised, StringComparison.Ordinal);
    }

    [Fact]
    public void QuotedLiteralsWithoutDigitsKeepTheirCaseAndContent()
    {
        // The specification forbids normalising these. "Local" and "addydeck" identify the run's
        // shape; folding them away would group failures that differ in exactly the way that matters.
        string normalised = MessageNormaliser.Normalise(RealFailureSamples.XunitAssertEmpty);

        Assert.Contains("\"Local\"", normalised, StringComparison.Ordinal);
        Assert.Contains("\"addydeck\"", normalised, StringComparison.Ordinal);
    }

    [Fact]
    public void AQuotedLiteralContainingDigitsIsNormalisedLikeAnythingElse()
    {
        // An id or a reading does not stop varying between runs because someone put quotes round it.
        string normalised = MessageNormaliser.Normalise("expected id \"order-4821\" to be present");

        Assert.Equal("expected id \"order-<num>\" to be present", normalised);
    }

    [Fact]
    public void NUnitsIndentAndConstraintBlockCollapseToOneLine()
    {
        string normalised = MessageNormaliser.Normalise(RealFailureSamples.NUnitAssertThat);

        Assert.StartsWith("watchdog (<num> ms)", normalised, StringComparison.Ordinal);
        Assert.Contains("assert.that(winner == servicecall, is.true) expected: true but was: false", normalised, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", normalised, StringComparison.Ordinal);
    }

    [Fact]
    public void AThrownExceptionMessageIsLeftAloneApartFromCase()
    {
        Assert.Equal(
            "this is a test exception for tracking purposes.",
            MessageNormaliser.Normalise(RealFailureSamples.XunitThrownException));
    }

    [Fact]
    public void NUnitsTypePrefixedMessageKeepsTheTypeName()
    {
        // NUnit writes the exception type into the message where xUnit does not. The type is
        // diagnostic, so it survives — normalising type names would collapse every thrown exception
        // in the suite into one signature.
        Assert.Equal(
            "system.invalidoperationexception : this is a test exception for tracking purposes.",
            MessageNormaliser.Normalise(RealFailureSamples.NUnitThrownException));
    }

    [Theory]
    [InlineData("run 3f2504e0-4f89-11d3-9a0c-0305e82c3301 failed", "run <guid> failed")]
    [InlineData("GET https://api.example.com/v2/orders?id=7 timed out", "get <uri> timed out")]
    [InlineData("could not open /Users/adrian/Dev/app/config.json", "could not open <path>")]
    [InlineData(@"could not open C:\Users\adrian\app\config.json", "could not open <path>")]
    [InlineData("expected 2026-08-10T20:08:30Z", "expected <time>")]
    [InlineData("expected 2026-08-10 but got 2026-08-11", "expected <time> but got <time>")]
    [InlineData("handle 0xDEADBEEF was closed", "handle <hex> was closed")]
    [InlineData("digest 7e5e7382baaeaa09 mismatched", "digest <hex> mismatched")]
    [InlineData("expected 42 but got 43.75", "expected <num> but got <num>")]
    [InlineData("version 10.0.5 is unsupported", "version <num> is unsupported")]
    public void EachRuleReplacesWhatItOwns(string input, string expected) =>
        Assert.Equal(expected, MessageNormaliser.Normalise(input));

    [Theory]
    [InlineData("Nullable`1 was null", "nullable`1 was null")]
    [InlineData("SHA256 mismatch in Utf8Formatter", "sha256 mismatch in utf8formatter")]
    [InlineData("<>c__DisplayClass47_0 captured", "<>c__displayclass47_0 captured")]
    public void DigitsBelongingToANameAreNotNumbers(string input, string expected) =>
        Assert.Equal(expected, MessageNormaliser.Normalise(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n  \t ")]
    public void AnAbsentMessageNormalisesToTheEmptyString(string? message) =>
        Assert.Equal(string.Empty, MessageNormaliser.Normalise(message));

    [Fact]
    public void LineEndingsDoNotChangeTheResult()
    {
        // The xUnit adapter joins an exception's messages with Environment.NewLine, so the same
        // failure recorded on Windows and on macOS differs by a carriage return. Two developers on
        // two platforms must still see one signature.
        string windows = MessageNormaliser.Normalise("first line\r\nsecond line");
        string unix = MessageNormaliser.Normalise("first line\nsecond line");

        Assert.Equal(unix, windows);
        Assert.Equal("first line second line", unix);
    }

    [Fact]
    public void NormalisingIsIdempotent()
    {
        // The report must be byte-identical between runs, and a signature is recomputed from stored
        // text on every one of them. A rule that kept rewriting its own output would drift.
        string once = MessageNormaliser.Normalise(RealFailureSamples.XunitAssertEmpty);

        Assert.Equal(once, MessageNormaliser.Normalise(once));
    }
}
