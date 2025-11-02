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
    /// Gets or sets the unique identifier for this test execution.
    /// </summary>
    public Guid ExecutionId { get; set; }

    /// <summary>
    /// Gets or sets the test identifier (framework-specific).
    /// </summary>
    public string TestId { get; set; }

    /// <summary>
    /// Gets or sets the test name.
    /// </summary>
    public string TestName { get; set; }

    /// <summary>
    /// Gets or sets the fully qualified test name including namespace and class.
    /// </summary>
    public string FullyQualifiedName { get; set; }

    /// <summary>
    /// Gets or sets the assembly name where the test is defined.
    /// </summary>
    public string Assembly { get; set; }

    /// <summary>
    /// Gets or sets the namespace where the test is defined.
    /// </summary>
    public string Namespace { get; set; }

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
    /// Gets or sets the environment information where the test was executed.
    /// </summary>
    public EnvironmentInfo Environment { get; set; }

    /// <summary>
    /// Gets or sets the test metadata including categories, tags, and custom attributes.
    /// </summary>
    public TestMetadata Metadata { get; set; }

    /// <summary>
    /// Gets or sets the error message if the test failed.
    /// </summary>
    public string ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the stack trace if the test failed.
    /// </summary>
    public string StackTrace { get; set; }
}
