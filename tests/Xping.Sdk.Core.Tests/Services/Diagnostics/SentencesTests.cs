/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Services.Diagnostics;

namespace Xping.Sdk.Core.Tests.Services.Diagnostics;

public sealed class SentencesTests
{
    [Theory]
    [InlineData("ApiKey is required in Cloud mode.", "ApiKey is required in Cloud mode.")]
    [InlineData("host failed to build", "host failed to build.")]
    [InlineData("  padded  ", "padded.")]
    [InlineData("Really?", "Really?")]
    [InlineData("Stop!", "Stop!")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void End_AddsAPeriodOnlyWhenTheTextHasNoEnding(string? text, string expected) =>
        Assert.Equal(expected, Sentences.End(text));

    [Fact]
    public void Join_EndsEachMessageOnceAndSkipsBlankOnes() =>
        Assert.Equal(
            "ApiKey is required in Cloud mode. BatchSize cannot exceed 1000.",
            Sentences.Join(["ApiKey is required in Cloud mode.", " ", "BatchSize cannot exceed 1000"]));
}
