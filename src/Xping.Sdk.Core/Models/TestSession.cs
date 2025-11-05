/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Models;

using System;
using System.Collections.ObjectModel;

/// <summary>
/// Represents a test session containing environment information and multiple test executions.
/// A session groups all tests executed together in a single test run, capturing the shared
/// environment context.
/// </summary>
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
        TestExecutions = [];
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

    /// <summary>
    /// Gets the collection of test executions in this session.
    /// </summary>
    internal Collection<TestExecution> TestExecutions { get; }
}
