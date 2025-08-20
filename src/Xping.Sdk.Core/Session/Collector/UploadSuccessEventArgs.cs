/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Session.Collector;

/// <summary>
/// Provides data for the <see cref="TestAgent.UploadSucceeded"/> event.
/// </summary>
public class UploadSuccessEventArgs : EventArgs
{
    /// <summary>
    /// Gets the test session that was successfully uploaded.
    /// </summary>
    public TestSession Session { get; }

    /// <summary>
    /// Gets the timestamp when the test session was uploaded.
    /// </summary>
    public DateTime UploadedAt { get; }

    /// <summary>
    /// Gets the upload token that was used for the upload.
    /// </summary>
    public Guid UploadToken { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="UploadSuccessEventArgs"/> class.
    /// </summary>
    /// <param name="session">The test session that was successfully uploaded.</param>
    /// <param name="uploadedAt">The timestamp when the test session was uploaded.</param>
    /// <param name="uploadToken">The upload token that was used for the upload.</param>
    public UploadSuccessEventArgs(TestSession session, DateTime uploadedAt, Guid uploadToken)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        UploadedAt = uploadedAt;
        UploadToken = uploadToken;
    }
}