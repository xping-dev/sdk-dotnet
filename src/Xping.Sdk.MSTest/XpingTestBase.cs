/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.MSTest;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xping.Sdk.Core.Environment;
using Xping.Sdk.Core.Models;

/// <summary>
/// Base class for MSTest tests that provides automatic test execution tracking.
/// Inherit from this class to enable automatic tracking of all test methods.
/// </summary>
public abstract class XpingTestBase
{
    private DateTime _startTime;
    private long _startTimestamp;

    /// <summary>
    /// Gets or sets the test context which provides information about and functionality for the current test run.
    /// </summary>
    public TestContext? TestContext { get; set; }

    /// <summary>
    /// Called before each test method executes. Starts timing the test.
    /// </summary>
    [TestInitialize]
    public void XpingTestInitialize()
    {
        _startTime = DateTime.UtcNow;
        _startTimestamp = Stopwatch.GetTimestamp();
    }

    /// <summary>
    /// Called after each test method executes. Records the test execution.
    /// </summary>
    [TestCleanup]
    public void XpingTestCleanup()
    {
        if (TestContext == null)
        {
            return;
        }

        var endTimestamp = Stopwatch.GetTimestamp();
        var endTime = DateTime.UtcNow;
        var duration = CalculateDuration(_startTimestamp, endTimestamp);

        var execution = CreateTestExecution(TestContext, _startTime, endTime, duration);
        XpingContext.RecordTest(execution);
    }

    /// <summary>
    /// Creates a TestExecution object from the MSTest TestContext.
    /// </summary>
    /// <param name="context">The MSTest test context.</param>
    /// <param name="startTime">The test start time.</param>
    /// <param name="endTime">The test end time.</param>
    /// <param name="duration">The test duration.</param>
    /// <returns>A populated TestExecution object.</returns>
    private static TestExecution CreateTestExecution(
        TestContext context,
        DateTime startTime,
        DateTime endTime,
        TimeSpan duration)
    {
        var outcome = MapOutcome(context.CurrentTestOutcome);
        var metadata = ExtractMetadata(context);

        // Extract namespace from fully qualified class name
        var className = context.FullyQualifiedTestClassName ?? string.Empty;
        var lastDotIndex = className.LastIndexOf('.');
        var namespaceName = lastDotIndex >= 0 ? className.Substring(0, lastDotIndex) : string.Empty;

        var detector = new EnvironmentDetector();
        var config = XpingContext.Configuration;
        var collectNetworkMetrics = config?.CollectNetworkMetrics ?? false;
        var apiEndpoint = config?.ApiEndpoint;

        return new TestExecution
        {
            ExecutionId = Guid.NewGuid(),
            TestId = $"{context.FullyQualifiedTestClassName}.{context.TestName}",
            TestName = context.TestName ?? "Unknown",
            FullyQualifiedName = $"{context.FullyQualifiedTestClassName}.{context.TestName}",
            Assembly = ExtractAssemblyName(className),
            Namespace = namespaceName,
            Outcome = outcome,
            Duration = duration,
            StartTimeUtc = startTime,
            EndTimeUtc = endTime,
            Environment = detector.Detect(collectNetworkMetrics, apiEndpoint),
            Metadata = metadata,
            ErrorMessage = GetErrorMessage(context),
            StackTrace = GetStackTrace(context)
        };
    }

    /// <summary>
    /// Maps MSTest outcome to Xping test outcome.
    /// </summary>
    /// <param name="outcome">The MSTest test outcome.</param>
    /// <returns>The corresponding Xping test outcome.</returns>
    private static TestOutcome MapOutcome(UnitTestOutcome outcome)
    {
        return outcome switch
        {
            UnitTestOutcome.Passed => TestOutcome.Passed,
            UnitTestOutcome.Failed => TestOutcome.Failed,
            UnitTestOutcome.Inconclusive => TestOutcome.Inconclusive,
            UnitTestOutcome.Timeout => TestOutcome.Failed,
            UnitTestOutcome.Aborted => TestOutcome.Failed,
            UnitTestOutcome.Unknown => TestOutcome.NotExecuted,
            UnitTestOutcome.NotRunnable => TestOutcome.NotExecuted,
            UnitTestOutcome.InProgress => TestOutcome.NotExecuted,
            _ => TestOutcome.NotExecuted
        };
    }

    /// <summary>
    /// Extracts metadata from the test context.
    /// </summary>
    /// <param name="context">The test context.</param>
    /// <returns>Test metadata including categories, properties, and framework tag.</returns>
    private static TestMetadata ExtractMetadata(TestContext context)
    {
        var categories = new List<string>();
        var tags = new List<string> { "framework:mstest" };
        var metadata = new TestMetadata
        {
            Categories = categories,
            Tags = tags,
            Description = string.Empty
        };

        // Extract test properties
        if (context.Properties != null)
        {
            foreach (var key in context.Properties.Keys)
            {
                var keyStr = key.ToString();
                if (string.IsNullOrEmpty(keyStr))
                {
                    continue;
                }

                var value = context.Properties[key]?.ToString() ?? string.Empty;

                // TestCategory is a special property
                if (keyStr == "TestCategory")
                {
                    categories.Add(value);
                }
                else
                {
                    metadata.CustomAttributes[keyStr] = value;
                    tags.Add($"{keyStr}:{value}");
                }
            }
        }

        // Check if this is a data-driven test by looking for data in properties
        var hasDataRow = false;
        if (context.Properties != null && context.Properties.Contains("DataRow"))
        {
            hasDataRow = true;
            var dataRowInfo = context.Properties["DataRow"]?.ToString();
            if (dataRowInfo != null && !string.IsNullOrEmpty(dataRowInfo))
            {
                metadata.CustomAttributes["Arguments"] = dataRowInfo;
                tags.Add("type:datatest");
            }
        }

        if (!hasDataRow)
        {
            tags.Add("type:test");
        }

        // Add test result files if present
        if (context.ResultsDirectory != null)
        {
            metadata.CustomAttributes["ResultsDirectory"] = context.ResultsDirectory;
        }

        return metadata;
    }

    /// <summary>
    /// Extracts assembly name from the fully qualified class name.
    /// </summary>
    /// <param name="fullyQualifiedClassName">The fully qualified class name.</param>
    /// <returns>The assembly name or empty string.</returns>
    private static string ExtractAssemblyName(string fullyQualifiedClassName)
    {
        if (string.IsNullOrEmpty(fullyQualifiedClassName))
        {
            return string.Empty;
        }

        // Assembly name is typically the first part before the first dot
        var firstDotIndex = fullyQualifiedClassName.IndexOf('.');
        return firstDotIndex >= 0 ? fullyQualifiedClassName.Substring(0, firstDotIndex) : fullyQualifiedClassName;
    }

    /// <summary>
    /// Gets the error message from a failed test.
    /// </summary>
    /// <param name="context">The test context.</param>
    /// <returns>The error message or null.</returns>
    private static string? GetErrorMessage(TestContext context)
    {
        if (context.CurrentTestOutcome == UnitTestOutcome.Passed)
        {
            return null;
        }

        // Try to get exception message from properties
        if (context.Properties != null && context.Properties.Contains("ExceptionMessage"))
        {
            return context.Properties["ExceptionMessage"]?.ToString();
        }

        return null;
    }

    /// <summary>
    /// Gets the stack trace from a failed test.
    /// </summary>
    /// <param name="context">The test context.</param>
    /// <returns>The stack trace or null.</returns>
    private static string? GetStackTrace(TestContext context)
    {
        if (context.CurrentTestOutcome == UnitTestOutcome.Passed)
        {
            return null;
        }

        // Try to get stack trace from properties
        if (context.Properties != null && context.Properties.Contains("StackTrace"))
        {
            return context.Properties["StackTrace"]?.ToString();
        }

        return null;
    }

    /// <summary>
    /// Calculates the duration using high-resolution timestamps.
    /// </summary>
    /// <param name="startTimestamp">Start timestamp from Stopwatch.GetTimestamp().</param>
    /// <param name="endTimestamp">End timestamp from Stopwatch.GetTimestamp().</param>
    /// <returns>The elapsed time as a TimeSpan.</returns>
    private static TimeSpan CalculateDuration(long startTimestamp, long endTimestamp)
    {
        var elapsedTicks = endTimestamp - startTimestamp;
        var ticksPerSecond = TimeSpan.TicksPerSecond;
        var durationTicks = (long)((double)elapsedTicks / Stopwatch.Frequency * ticksPerSecond);
        return new TimeSpan(durationTicks);
    }
}
