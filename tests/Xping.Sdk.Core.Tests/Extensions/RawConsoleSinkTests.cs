/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text;
using Xping.Sdk.Core.Extensions.Internals;

namespace Xping.Sdk.Core.Tests.Extensions;

public sealed class RawConsoleSinkTests
{
    [Fact]
    public void TheStreamWritersWriteNoByteOrderMark()
    {
        // With a byte order mark, the SDK's first line on stdout and on stderr started with U+FEFF in
        // the test runner's output. Both streams are opened through CreateWriter.
        using var stream = new MemoryStream();
        using (StreamWriter writer = RawConsoleSink.CreateWriter(stream))
        {
            writer.Write("[Xping]");
        }

        Assert.Equal(Encoding.ASCII.GetBytes("[Xping]"), stream.ToArray());
    }
}
