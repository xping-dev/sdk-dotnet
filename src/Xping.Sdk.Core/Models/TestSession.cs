/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Models;

using System;

/// <summary>
/// Represents a test session containing environment information needed for flakiness detection and confidence scoring.
/// </summary>
/// <remarks>
/// In batch operations, this context is sent once with the first execution to optimize
/// payload size, as all executions in a batch share the same session environment.
/// </remarks>
public sealed class TestSession
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestSession"/> class.
    /// </summary>
    public TestSession()
    {
        SessionId = Guid.NewGuid().ToString();
        StartedAt = DateTime.UtcNow;
        EnvironmentInfo = new EnvironmentInfo();
    }

    /// <summary>
    /// Gets or sets the unique identifier for this test session.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets when the test session started (UTC).
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// Gets or sets when the test session completed (UTC).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Gets or sets the environment information for this test session.
    /// This is shared across all test executions in the session.
    /// </summary>
    public EnvironmentInfo EnvironmentInfo { get; set; }
}
