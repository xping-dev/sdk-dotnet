/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using Xping.Sdk.Core.Session;
using Xping.Sdk.Core.Session.Collector;

namespace Xping.Sdk.Core;

/// <summary>
/// Provides data for the <see cref="TestAgent.UploadFailed"/> event.
/// </summary>
public class UploadFailedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the test session that failed to upload.
    /// </summary>
    public TestSession Session { get; }

    /// <summary>
    /// Gets the upload result containing status information.
    /// </summary>
    public UploadResult UploadResult { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="UploadFailedEventArgs"/> class.
    /// </summary>
    /// <param name="session">The test session that failed to upload.</param>
    /// <param name="result">The upload result containing status information.</param>
    public UploadFailedEventArgs(TestSession session, UploadResult result)
    {
        Session = session;
        UploadResult = result;
    }
}
