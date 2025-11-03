/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Models;

using System;

/// <summary>
/// Represents a single test execution with all associated metadata and results.
/// </summary>
public sealed class TestExecution
{
    /// <summary>
    /// Gets or sets the unique identifier for this test execution instance.
    /// This changes with each execution of the test.
    /// </summary>
    public Guid ExecutionId { get; set; }

    /// <summary>
    /// Gets or sets the stable test identity that persists across runs.
    /// This is the primary identifier for tracking tests over time.
    /// </summary>
    /// <remarks>
    /// The TestIdentity contains a stable hash-based ID that remains constant
    /// for the same test across different environments, machines, and runs.
    /// Use this for historical analysis and tracking test reliability.
    /// </remarks>
    public TestIdentity Identity { get; set; } = new TestIdentity();

    /// <summary>
    /// Gets or sets the test name.
    /// </summary>
    public string TestName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the outcome of the test execution.
    /// </summary>
    public TestOutcome Outcome { get; set; }

    /// <summary>
    /// Gets or sets the duration of the test execution.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets or sets the start time of the test execution in UTC.
    /// </summary>
    public DateTime StartTimeUtc { get; set; }

    /// <summary>
    /// Gets or sets the end time of the test execution in UTC.
    /// </summary>
    public DateTime EndTimeUtc { get; set; }

    /// <summary>
    /// Gets or sets the session ID that this test execution belongs to.
    /// This links the test execution to its parent session which contains the environment information.
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// Gets or sets the test metadata including categories, tags, and custom attributes.
    /// </summary>
    public TestMetadata? Metadata { get; set; }

    /// <summary>
    /// Gets or sets the error message if the test failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the stack trace if the test failed.
    /// </summary>
    public string? StackTrace { get; set; }
}
