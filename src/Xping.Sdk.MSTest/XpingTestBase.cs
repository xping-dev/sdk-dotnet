/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.MSTest;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xping.Sdk.Core.Models;

/// <summary>
/// Base class for MSTest tests that provides automatic test execution tracking.
/// Inherit from this class to enable automatic tracking of all test methods.
/// </summary>
/// <remarks>
/// <para>
/// This base class only tracks tests that actually execute. Tests marked with
/// the [Ignore] attribute are skipped by MSTest before execution begins and
/// will not be tracked by Xping.
/// </para>
/// </remarks>
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
    /// Cleans up after each test and records the test execution.
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

        // Extract thread ID and class name for execution tracking
        var threadId = Environment.CurrentManagedThreadId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var className = TestContext.FullyQualifiedTestClassName ?? "Unknown";

        var execution = CreateTestExecution(TestContext, _startTime, endTime, duration, threadId, className);
        XpingContext.RecordTest(execution);
    }

    /// <summary>
    /// Creates a TestExecution object from the MSTest TestContext.
    /// </summary>
    /// <param name="context">The MSTest test context.</param>
    /// <param name="startTime">The test start time.</param>
    /// <param name="endTime">The test end time.</param>
    /// <param name="duration">The test duration.</param>
    /// <param name="threadId">The thread ID for execution tracking.</param>
    /// <param name="className">The fully qualified class name for execution tracking.</param>
    /// <returns>A populated TestExecution object.</returns>
    private static TestExecution CreateTestExecution(
        TestContext context,
        DateTime startTime,
        DateTime endTime,
        TimeSpan duration,
        string threadId,
        string className)
    {
        var outcome = MapOutcome(context.CurrentTestOutcome);
        var metadata = ExtractMetadata(context);

        // Detect retry metadata using MSTestRetryDetector
        var retryDetector = new Xping.Sdk.MSTest.Retry.MSTestRetryDetector();
        var retryMetadata = retryDetector.DetectRetryMetadata(context);

        // If retry is detected and test outcome is passed, update PassedOnRetry flag
        if (retryMetadata != null && outcome == TestOutcome.Passed)
        {
            var attemptNumber = retryDetector.GetCurrentAttemptNumber(context);
            if (attemptNumber > 1)
            {
                retryMetadata.PassedOnRetry = true;
            }
        }

        // Extract assembly from fully qualified class name
        var fullClassName = context.FullyQualifiedTestClassName ?? string.Empty;
        var assemblyName = ExtractAssemblyName(fullClassName);

        // Generate stable test identity
        var fullyQualifiedName = $"{context.FullyQualifiedTestClassName}.{context.TestName}";

        // Extract DataRow parameters if present
        // MSTest exposes parameterized test arguments through TestContext.DataRow (System.Data.DataRow)
        object[]? parameters = ExtractDataRowParameters(context);

        // Format test name with parameters to match NUnit and xUnit behavior
        var testName = FormatTestNameWithParameters(context.TestName ?? "Unknown", parameters);

        var identity = TestIdentityGenerator.Generate(
            fullyQualifiedName,
            assemblyName,
            parameters,
            testName);

        // Create execution context using the ExecutionTracker
        var executionContext = XpingContext.ExecutionTracker?.CreateContext(threadId, className);

        var testExecution = new TestExecution
        {
            ExecutionId = Guid.NewGuid(),
            Identity = identity,
            TestName = testName,
            Outcome = outcome,
            Duration = duration,
            StartTimeUtc = startTime,
            EndTimeUtc = endTime,
            SessionContext = XpingContext.CurrentSession,
            Metadata = metadata,
            Retry = retryMetadata,
            ExceptionType = GetExceptionType(context),
            ErrorMessage = GetErrorMessage(context),
            StackTrace = GetStackTrace(context),
            ErrorMessageHash = TestIdentityGenerator.GenerateErrorMessageHash(GetErrorMessage(context)),
            StackTraceHash = TestIdentityGenerator.GenerateStackTraceHash(GetStackTrace(context)),
            Context = executionContext
        };

        // Record test completion for execution tracking
        XpingContext.ExecutionTracker?.RecordTestCompletion(
            threadId,
            testExecution.Identity.TestId,
            testName,
            outcome);

        return testExecution;
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
    /// Gets the exception type from a failed test.
    /// </summary>
    /// <param name="context">The test context.</param>
    /// <returns>The exception type name or null.</returns>
    private static string? GetExceptionType(TestContext context)
    {
        if (context.CurrentTestOutcome == UnitTestOutcome.Passed)
        {
            return null;
        }

        // Try to get exception type from properties
        if (context.Properties != null && context.Properties.Contains("ExceptionType"))
        {
            return context.Properties["ExceptionType"]?.ToString();
        }

        return null;
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
    /// Extracts DataRow parameters from the MSTest TestContext.
    /// </summary>
    /// <param name="context">The test context.</param>
    /// <returns>An array of parameter values, or null if not a parameterized test.</returns>
    /// <remarks>
    /// MSTest exposes DataRow parameters through TestContext.TestData (introduced in MSTest 3.7).
    /// This is different from NUnit (uses Properties["Arguments"]) and xUnit (uses TestMethodArguments).
    ///
    /// For DataTestMethod tests with DataRow attributes:
    /// - TestContext.TestData contains the parameter values as object[]
    /// - This enables unique test IDs for each DataRow variation
    /// - For non-parameterized tests, TestData is null
    ///
    /// Legacy note: TestContext.DataRow (System.Data.DataRow) is only for [DataSource] attribute,
    /// not for modern [DataRow] attributes.
    /// </remarks>
    private static object[]? ExtractDataRowParameters(TestContext context)
    {
        if (context == null)
        {
            return null;
        }

        try
        {
            // TestContext.TestData contains the parameters for DataRow-based tests (MSTest 3.7+)
            // For non-parameterized tests, TestData is null
            var testData = context.TestData;

            // Return null if empty to avoid generating parameter hash for parameterless tests
            // Cast to object[] to match the expected return type (TestData is object?[])
            return testData != null && testData.Length > 0 ? (object[])testData : null;
        }
        catch
        {
            // If we fail to extract parameters, return null to use base test identity
            // This ensures we don't break test tracking if MSTest internals change
            return null;
        }
    }

    /// <summary>
    /// Formats a test name with parameters to match NUnit and xUnit display format.
    /// </summary>
    /// <param name="baseTestName">The base test method name.</param>
    /// <param name="parameters">The test parameters, or null for non-parameterized tests.</param>
    /// <returns>The formatted test name with parameters in parentheses (e.g., "TestMethod(1,2,3)").</returns>
    /// <remarks>
    /// MSTest's TestContext.TestName does not include parameters, unlike NUnit and xUnit.
    /// This method formats the test name to match the pattern used by other frameworks,
    /// making it easier to identify specific test cases on the Xping dashboard.
    ///
    /// Examples:
    /// - Non-parameterized: "Add_TwoNumbers_ReturnsSum" → "Add_TwoNumbers_ReturnsSum"
    /// - Parameterized: "Add_MultipleInputs_ReturnsExpectedSum" + [2,3,5] → "Add_MultipleInputs_ReturnsExpectedSum (2,3,5)"
    /// </remarks>
    private static string FormatTestNameWithParameters(string baseTestName, object[]? parameters)
    {
        if (parameters == null || parameters.Length == 0)
        {
            return baseTestName;
        }

        // Format parameters using the same logic as TestIdentityGenerator
        var formattedParams = new System.Collections.Generic.List<string>();
        foreach (var param in parameters)
        {
            formattedParams.Add(FormatParameterValue(param));
        }

        var paramString = string.Join(",", formattedParams);
        return $"{baseTestName} ({paramString})";
    }

    /// <summary>
    /// Formats a single parameter value for display.
    /// </summary>
    /// <param name="parameter">The parameter value to format.</param>
    /// <returns>A string representation of the parameter.</returns>
    private static string FormatParameterValue(object? parameter)
    {
        return parameter switch
        {
            null => "null",
            string str => str,
            bool b => b ? "true" : "false",
            byte b => b.ToString(System.Globalization.CultureInfo.InvariantCulture),
            sbyte sb => sb.ToString(System.Globalization.CultureInfo.InvariantCulture),
            short s => s.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ushort us => us.ToString(System.Globalization.CultureInfo.InvariantCulture),
            int i => i.ToString(System.Globalization.CultureInfo.InvariantCulture),
            uint ui => ui.ToString(System.Globalization.CultureInfo.InvariantCulture),
            long l => l.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ulong ul => ul.ToString(System.Globalization.CultureInfo.InvariantCulture),
            float f => f.ToString("G9", System.Globalization.CultureInfo.InvariantCulture),
            double d => d.ToString("G17", System.Globalization.CultureInfo.InvariantCulture),
            decimal dec => dec.ToString(System.Globalization.CultureInfo.InvariantCulture),
            DateTime dt => dt.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", System.Globalization.CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToString("yyyy-MM-ddTHH:mm:ss.fffffffzzz", System.Globalization.CultureInfo.InvariantCulture),
            Guid g => g.ToString("D"),
            _ => parameter.ToString() ?? "null"
        };
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
