/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Tests.Helpers;

/// <summary>
/// A local-store directory of a test's own, deleted on dispose. Hosts built through the real
/// registration write local history at finalization, and without one it would land in the
/// repository's own <c>.xping/</c>.
/// </summary>
internal sealed class ScratchStore : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(), "xping-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(Path))
            Directory.Delete(Path, recursive: true);
    }
}
