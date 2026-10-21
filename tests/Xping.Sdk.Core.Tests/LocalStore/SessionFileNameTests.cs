/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Services.LocalStore.Internals;

namespace Xping.Sdk.Core.Tests.LocalStore;

public sealed class SessionFileNameTests
{
    private static readonly DateTime StartedAt = new(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid SessionId = Guid.Parse("47b1a20f-3d97-4c1e-9d0a-5f3e1f9b6544");

    [Fact]
    public void BuildCarriesTheStartTimeAndATagPerAssembly()
    {
        string name = SessionFileName.Build(StartedAt, SessionId, ["Alpha.Tests", "Beta.Tests"]);

        Assert.StartsWith("session-" + StartedAt.Ticks.ToString("D19", System.Globalization.CultureInfo.InvariantCulture) + "-47b1a20f-", name, StringComparison.Ordinal);
        Assert.EndsWith(".json.gz", name, StringComparison.Ordinal);
        Assert.Equal([SessionFileName.Tag("Alpha.Tests"), SessionFileName.Tag("Beta.Tests")], SessionFileName.ParseTags(name));
        Assert.True(SessionFileName.TryParseStartedAt(name, out DateTime parsed));
        Assert.Equal(StartedAt, parsed);
    }

    [Fact]
    public void BuildLeavesAWideOrEmptySessionUntagged()
    {
        string[] nine = [.. Enumerable.Range(0, 9).Select(i => $"A{i}.Tests")];

        Assert.Null(SessionFileName.ParseTags(SessionFileName.Build(StartedAt, SessionId, nine)));
        Assert.Null(SessionFileName.ParseTags(SessionFileName.Build(StartedAt, SessionId, [])));
    }

    [Fact]
    public void TagIsStableAndEightHexDigits()
    {
        Assert.Equal(SessionFileName.Tag("Alpha.Tests"), SessionFileName.Tag("Alpha.Tests"));
        Assert.NotEqual(SessionFileName.Tag("Alpha.Tests"), SessionFileName.Tag("Beta.Tests"));
        Assert.Matches("^[0-9a-f]{8}$", SessionFileName.Tag("Alpha.Tests"));
    }

    [Theory]
    [InlineData("session-0639281562955320130-47b1a20f.json.gz")]
    [InlineData("session-0639281562955320130-47b1a20f-xyz.json.gz")]
    [InlineData("session-0639281562955320130-47b1a20f-0123abcd.zz.json.gz")]
    [InlineData("other.json.gz")]
    public void ParseTagsReturnsNullForANameWithoutValidTags(string name)
    {
        Assert.Null(SessionFileName.ParseTags(name));
    }

    [Fact]
    public void TryParseStartedAtRejectsANameWithoutTicks()
    {
        Assert.False(SessionFileName.TryParseStartedAt("session-notaticks.json.gz", out _));
    }
}
