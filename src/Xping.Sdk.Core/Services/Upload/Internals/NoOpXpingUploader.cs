/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models;

namespace Xping.Sdk.Core.Services.Upload.Internals;

/// <summary>
/// A no-op implementation of <see cref="IXpingUploader"/> used when the SDK
/// is disabled or configuration validation fails. All upload operations are no-ops,
/// returning successful results without performing any network calls.
/// </summary>
internal sealed class NoOpXpingUploader : IXpingUploader
{
    /// <inheritdoc/>
    public Task<UploadResult> UploadAsync(
        TestSession testSession,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new UploadResult
        {
            Success = true,
            TotalRecordsCount = 0
        });
    }
}
