/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Components;

/// <summary>
/// This class is used to store settings for a test execution. It provides a set of properties that can be used to 
/// configure the behavior of the test run, such as the timeout duration or pipeline termination behavior.
/// </summary>
public sealed class TestSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether to continue running all tests in the pipline regardless of their 
    /// results. Default is false.
    /// </summary>
    /// <remarks>
    /// If this property is set to <c>true</c>, all tests will be run regardless of their results. If this property is 
    /// set to <c>false</c>, the testing pipeline will stop running tests when a failure occurs.
    /// </remarks>
    public bool ContinueOnFailure { get; set; }

    /// <summary>
    /// Gets or sets the maximum amount of time to allow a test operation to run. Default is 30 seconds.
    /// </summary>
    /// <value>
    /// The maximum execution time as a <see cref="System.TimeSpan"/> object.
    /// </value>
    /// <remarks>
    /// This property defines the upper limit on the duration a test can take. If the test exceeds this time,
    /// it should be considered failed or aborted. Setting this value helps ensure tests complete in an expected 
    /// timeframe.
    /// </remarks>
    public TimeSpan Timeout { get; set; } =
#if DEBUG
        TimeSpan.FromMinutes(30);
#else
        TimeSpan.FromSeconds(30);
#endif

    /// <summary>
    /// Gets or sets the attribute name used to uniquely identify HTML elements on the web page for testing purposes.
    /// This property is typically used in conjunction with automated testing tools to select elements by their test ID.
    /// The default value is 'data-testid', which is a common convention in web development.
    /// </summary>
    public string TestIdAttribute { get; set; } = "data-testid";
}
