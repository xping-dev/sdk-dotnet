/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Services.Upload.Internals;

namespace Xping.Sdk.Core.Tests.Services.Upload;

public sealed class NoOpXpingUploaderTests
{
    // ---------------------------------------------------------------------------
    // UploadAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task UploadAsync_WithValidSession_ReturnsSuccess()
    {
        // Arrange
        var uploader = new NoOpXpingUploader();
        var session = new TestSession();

        // Act
        var result = await uploader.UploadAsync(session);

        // Assert
        Assert.True(result.Success);
    }

    [Fact]
    public async Task UploadAsync_ReturnsZeroRecordsCount()
    {
        // Arrange
        var uploader = new NoOpXpingUploader();

        // Act
        var result = await uploader.UploadAsync(new TestSession());

        // Assert
        Assert.Equal(0, result.TotalRecordsCount);
    }

    [Fact]
    public async Task UploadAsync_WithCancelledToken_DoesNotThrow()
    {
        // Arrange
        var uploader = new NoOpXpingUploader();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert — no-op should not observe the cancellation token
        var ex = await Record.ExceptionAsync(() => uploader.UploadAsync(new TestSession(), cts.Token));
        Assert.Null(ex);
    }

    [Fact]
    public async Task UploadAsync_WithNullSession_DoesNotThrow()
    {
        // Arrange
        var uploader = new NoOpXpingUploader();

        // Act & Assert — no-op never inspects the session
        var ex = await Record.ExceptionAsync(() => uploader.UploadAsync(null!));
        Assert.Null(ex);
    }
}
