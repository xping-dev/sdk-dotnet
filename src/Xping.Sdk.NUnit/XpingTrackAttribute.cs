/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.NUnit;

using System;
using System.Diagnostics;
using global::NUnit.Framework;
using global::NUnit.Framework.Interfaces;
using Xping.Sdk.Core.Models;

/// <summary>
/// NUnit attribute for tracking test execution with Xping.
/// Can be applied to test methods or test fixtures.
/// </summary>
[AttributeUsage(
    AttributeTargets.Method
    | AttributeTargets.Class
    | AttributeTargets.Assembly, AllowMultiple = false, Inherited = true)]
public sealed class XpingTrackAttribute : Attribute, ITestAction
{
    private const string StartTimeKey = "Xping.StartTime";
    private const string StartTimestampKey = "Xping.StartTimestamp";
    private static readonly string[] LineSeparators = new[] { "\r\n", "\r", "\n" };

    /// <summary>
    /// Gets the action targets (Test level).
    /// </summary>
    public ActionTargets Targets => ActionTargets.Test;

    /// <summary>
    /// Called before each test execution.
    /// </summary>
    /// <param name="test">The test being executed.</param>
    public void BeforeTest(ITest test)
    {
        if (!XpingContext.IsInitialized)
        {
            XpingContext.Initialize();
        }

        if (test == null)
        {
            throw new ArgumentNullException(nameof(test), "Test cannot be null in BeforeTest.");
        }

        // Store timing data in test properties to avoid thread-safety issues
        // (attribute instances are reused for multiple tests)
        var startTime = DateTime.UtcNow;
        var startTimestamp = Stopwatch.GetTimestamp();

        test.Properties.Set(StartTimeKey, startTime);
        test.Properties.Set(StartTimestampKey, startTimestamp);
    }

    /// <summary>
    /// Called after each test execution.
    /// </summary>
    /// <param name="test">The test that was executed.</param>
    public void AfterTest(ITest test)
    {
        var endTime = DateTime.UtcNow;
        var endTimestamp = Stopwatch.GetTimestamp();

        if (test == null)
        {
            return;
        }

        try
        {
            // Retrieve timing data from test properties
            if (!test.Properties.ContainsKey(StartTimeKey) ||
                !test.Properties.ContainsKey(StartTimestampKey))
            {
                return;
            }

            var startTimeObj = test.Properties.Get(StartTimeKey);
            var startTimestampObj = test.Properties.Get(StartTimestampKey);

            if (startTimeObj == null || startTimestampObj == null)
            {
                return;
            }

            var startTime = (DateTime)startTimeObj;
            var startTimestamp = (long)startTimestampObj;

            // Calculate accurate duration using high-resolution timestamps
            var elapsedTicks = endTimestamp - startTimestamp;
            var duration = TimeSpan.FromTicks((long)(elapsedTicks * TimeSpan.TicksPerSecond / Stopwatch.Frequency));

            // Extract WorkerId from NUnit context (available for parallel execution)
            var workerId = TestContext.CurrentContext.WorkerId;
            var fixtureName = test.TypeInfo?.FullName ?? test.ClassName;

            var execution = CreateTestExecution(test, startTime, endTime, duration, workerId, fixtureName);
            XpingContext.RecordTest(execution);
        }
        catch
        {
            // Swallow exceptions to avoid interfering with test execution
        }
    }

    private static TestExecution CreateTestExecution(
        ITest test,
        DateTime startTime,
        DateTime endTime,
        TimeSpan duration,
        string? workerId,
        string? fixtureName)
    {
        var result = TestContext.CurrentContext.Result;

        // Generate stable test identity
        var fullyQualifiedName = test.FullName;
        var assemblyName = test.TypeInfo?.Assembly.GetName().Name ?? string.Empty;

        // Extract test case arguments if parameterized
        object[]? parameters = null;
        if (test.Properties.ContainsKey("Arguments"))
        {
            var args = test.Properties["Arguments"];
            if (args.Count > 0 && args[0] is object[] argsArray)
            {
                parameters = argsArray;
            }
        }

        var displayName = test.Name;

        var identity = TestIdentityGenerator.Generate(
            fullyQualifiedName,
            assemblyName,
            parameters,
            displayName);

        var errorMessage = result.Message ?? string.Empty;
        var stackTrace = result.StackTrace ?? string.Empty;
        var outcome = MapOutcome(result.Outcome);

        // Create execution context using ExecutionTracker
        var executionTracker = XpingContext.ExecutionTracker;
        var context = executionTracker?.CreateContext(workerId, fixtureName);

        var execution = new TestExecution
        {
            ExecutionId = Guid.NewGuid(),
            Identity = identity,
            TestName = test.Name,
            Outcome = outcome,
            Duration = duration,
            StartTimeUtc = startTime,
            EndTimeUtc = endTime,
            SessionContext = XpingContext.CurrentSession,
            Metadata = ExtractMetadata(test),
            ExceptionType = ExtractExceptionType(result),
            ErrorMessage = errorMessage,
            StackTrace = stackTrace,
            ErrorMessageHash = TestIdentityGenerator.GenerateErrorMessageHash(errorMessage),
            StackTraceHash = TestIdentityGenerator.GenerateStackTraceHash(stackTrace),
            Context = context
        };

        // Record test completion for tracking as previous test
        executionTracker?.RecordTestCompletion(workerId, identity.TestId, test.Name, outcome);

        return execution;
    }

    private static string? ExtractExceptionType(TestContext.ResultAdapter result)
    {
        // NUnit 3 doesn't expose the exception type directly through the public API
        // We need to parse it from the message or stack trace
        if (result.Outcome.Status == TestStatus.Passed)
        {
            return null;
        }

        // Try to extract from message - often contains exception type
        var message = result.Message;
        if (!string.IsNullOrEmpty(message))
        {
            // Non-null after the check above
            var lines = message!.Split(LineSeparators, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length > 0)
            {
                var firstLine = lines[0].Trim();
                // Check if it looks like an exception type (contains namespace and ends with Exception)
                if (firstLine.Contains('.') && firstLine.Contains("Exception"))
                {
                    // Extract just the exception type, not the full message
                    var colonIndex = firstLine.IndexOf(':');
                    if (colonIndex > 0)
                    {
                        return firstLine.Substring(0, colonIndex).Trim();
                    }
                    // If no colon, the whole line might be the exception type
                    if (!firstLine.Contains(' '))
                    {
                        return firstLine;
                    }
                }
            }
        }

        return null;
    }

    private static TestOutcome MapOutcome(ResultState resultState)
    {
        // NUnit 3 uses Success status
        if (resultState == ResultState.Success)
        {
            return TestOutcome.Passed;
        }

        if (resultState == ResultState.Failure ||
            resultState == ResultState.Error ||
            resultState == ResultState.SetUpFailure ||
            resultState == ResultState.SetUpError ||
            resultState == ResultState.TearDownError ||
            resultState == ResultState.ChildFailure)
        {
            return TestOutcome.Failed;
        }

        if (resultState == ResultState.Skipped ||
            resultState == ResultState.Ignored ||
            resultState == ResultState.Explicit)
        {
            return TestOutcome.Skipped;
        }

        if (resultState == ResultState.Inconclusive)
        {
            return TestOutcome.Inconclusive;
        }

        if (resultState == ResultState.NotRunnable ||
            resultState == ResultState.Cancelled)
        {
            return TestOutcome.NotExecuted;
        }

        return TestOutcome.NotExecuted;
    }

    private static TestMetadata ExtractMetadata(ITest test)
    {
        var categories = new System.Collections.Generic.List<string>();
        var tags = new System.Collections.Generic.List<string>();
        var customAttributes = new System.Collections.Generic.Dictionary<string, string>();
        string? description = null;

        // Extract categories
        if (test.Properties.ContainsKey("Category"))
        {
            var categoryValues = test.Properties["Category"];
            foreach (var category in categoryValues)
            {
                if (category != null)
                {
                    categories.Add(category.ToString()!);
                }
            }
        }

        // Extract description
        if (test.Properties.ContainsKey("Description"))
        {
            var descriptions = test.Properties["Description"];
            if (descriptions.Count > 0 && descriptions[0] != null)
            {
                description = descriptions[0].ToString();
            }
        }

        // Extract author
        if (test.Properties.ContainsKey("Author"))
        {
            var authors = test.Properties["Author"];
            foreach (var author in authors)
            {
                if (author != null)
                {
                    tags.Add($"author:{author}");
                }
            }
        }

        // Extract test case arguments if parameterized
        if (test.Properties.ContainsKey("Arguments"))
        {
            var args = test.Properties["Arguments"];
            if (args.Count > 0 && args[0] != null)
            {
                customAttributes.Add("Arguments", args[0].ToString()!);
            }
        }

        // Add framework identifier
        tags.Add("framework:nunit");

        // Add test type
        if (test.IsSuite)
        {
            tags.Add("type:suite");
        }
        else
        {
            tags.Add("type:test");
        }

        // Add fixture type name if available
        if (test.TypeInfo != null)
        {
            customAttributes.Add("FixtureType", test.TypeInfo.FullName ?? test.TypeInfo.Name);
        }

        var metadata = new TestMetadata
        {
            Categories = categories,
            Tags = tags,
            Description = description,
        };

        // Copy custom attributes
        foreach (var kvp in customAttributes)
        {
            metadata.CustomAttributes[kvp.Key] = kvp.Value;
        }

        return metadata;
    }
}
