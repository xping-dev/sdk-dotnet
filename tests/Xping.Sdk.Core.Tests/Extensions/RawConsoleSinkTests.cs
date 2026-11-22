/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Extensions.Internals;

namespace Xping.Sdk.Core.Tests.Extensions;

public sealed class RawConsoleSinkTests
{
    [Fact]
    public void TheStreamsAreWrittenWithoutAByteOrderMark()
    {
        // With a byte order mark, the SDK's first line on stdout and on stderr started with U+FEFF in
        // the test runner's output.
        Assert.Empty(RawConsoleSink.StreamEncoding.GetPreamble());
        Assert.Equal("utf-8", RawConsoleSink.StreamEncoding.WebName);
    }
}
