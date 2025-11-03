/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.XUnit;

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using global::Xunit.Abstractions;
using Xping.Sdk.Core.Models;

#pragma warning disable CA1305 // Specify IFormatProvider for culture-aware ToString conversions

/// <summary>
/// Message sink that intercepts xUnit test execution messages and records them to Xping.
/// Tracks test start/end times and outcomes in a thread-safe manner.
/// </summary>
public sealed class XpingMessageSink : IMessageSink
{
    private readonly IMessageSink _innerSink;
    private readonly ConcurrentDictionary<string, TestExecutionData> _testData;

    /// <summary>
    /// Initializes a new instance of the <see cref="XpingMessageSink"/> class.
    /// </summary>
    /// <param name="innerSink">The inner message sink to forward messages to.</param>
    public XpingMessageSink(IMessageSink innerSink)
    {
        _innerSink = innerSink ?? throw new ArgumentNullException(nameof(innerSink));
        _testData = new ConcurrentDictionary<string, TestExecutionData>();
    }

    /// <summary>
    /// Handles incoming messages from xUnit test execution.
    /// </summary>
    /// <param name="message">The message to process.</param>
    /// <returns>True if the message was processed successfully.</returns>
    public bool OnMessage(IMessageSinkMessage message)
    {
        // Handle test lifecycle messages
        switch (message)
        {
            case ITestStarting testStarting:
                HandleTestStarting(testStarting);
                break;

            case ITestPassed testPassed:
                HandleTestPassed(testPassed);
                break;

            case ITestFailed testFailed:
                HandleTestFailed(testFailed);
                break;

            case ITestSkipped testSkipped:
                HandleTestSkipped(testSkipped);
                break;

            case ITestAssemblyFinished _:
                // When assembly finishes, flush all recorded test data
                HandleTestAssemblyFinished();
                break;
        }

        // Forward to inner sink
        return _innerSink.OnMessage(message);
    }

    private void HandleTestStarting(ITestStarting testStarting)
    {
        var testKey = GetTestKey(testStarting.Test);
        var data = new TestExecutionData
        {
            Test = testStarting.Test,
            StartTime = DateTime.UtcNow,
            StartTimestamp = Stopwatch.GetTimestamp(),
        };

        _testData.TryAdd(testKey, data);
    }

    private void HandleTestPassed(ITestPassed testPassed)
    {
        var testKey = GetTestKey(testPassed.Test);
        if (!_testData.TryRemove(testKey, out var data))
        {
            return;
        }

        var endTime = DateTime.UtcNow;
        var endTimestamp = Stopwatch.GetTimestamp();
        var duration = CalculateDuration(data.StartTimestamp, endTimestamp);

        RecordTestExecution(
            test: data.Test,
            outcome: TestOutcome.Passed,
            startTime: data.StartTime,
            endTime: endTime,
            duration: duration,
            executionTime: testPassed.ExecutionTime,
            output: testPassed.Output,
            errorMessage: null,
            stackTrace: null);
    }

    private void HandleTestFailed(ITestFailed testFailed)
    {
        var testKey = GetTestKey(testFailed.Test);
        if (!_testData.TryRemove(testKey, out var data))
        {
            return;
        }

        var endTime = DateTime.UtcNow;
        var endTimestamp = Stopwatch.GetTimestamp();
        var duration = CalculateDuration(data.StartTimestamp, endTimestamp);

        RecordTestExecution(
            test: data.Test,
            outcome: TestOutcome.Failed,
            startTime: data.StartTime,
            endTime: endTime,
            duration: duration,
            executionTime: testFailed.ExecutionTime,
            output: testFailed.Output,
            errorMessage: string.Join(Environment.NewLine, testFailed.Messages),
            stackTrace: string.Join(Environment.NewLine, testFailed.StackTraces));
    }

    private void HandleTestSkipped(ITestSkipped testSkipped)
    {
        var testKey = GetTestKey(testSkipped.Test);
        if (!_testData.TryRemove(testKey, out var data))
        {
            return;
        }

        var endTime = DateTime.UtcNow;
        var endTimestamp = Stopwatch.GetTimestamp();
        var duration = CalculateDuration(data.StartTimestamp, endTimestamp);

        RecordTestExecution(
            test: data.Test,
            outcome: TestOutcome.Skipped,
            startTime: data.StartTime,
            endTime: endTime,
            duration: duration,
            executionTime: 0,
            output: string.Empty,
            errorMessage: $"Test skipped: {testSkipped.Reason}",
            stackTrace: null
        );
    }

    private void HandleTestAssemblyFinished()
    {
        // Flush all recorded test data when assembly finishes executing
        // This ensures data is uploaded even when using VSTest adapter
        // (which doesn't call Dispose on custom frameworks or collection fixtures)
        try
        {
            XpingContext.FlushAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            // Log error but don't throw - we don't want to fail tests due to flush errors
            Debug.WriteLine($"[Xping] Error flushing data on assembly finished: {ex.Message}");
        }
    }

    private static void RecordTestExecution(
        ITest test,
        TestOutcome outcome,
        DateTime startTime,
        DateTime endTime,
        TimeSpan duration,
        decimal executionTime,
        string output,
        string? errorMessage,
        string? stackTrace)
    {
        try
        {
            var execution = CreateTestExecution(
                test,
                outcome,
                startTime,
                endTime,
                duration,
                executionTime,
                output,
                errorMessage,
                stackTrace);

            XpingContext.RecordTest(execution);
        }
        catch
        {
            // Swallow exceptions to avoid interfering with test execution
        }
    }

    private static TestExecution CreateTestExecution(
        ITest test,
        TestOutcome outcome,
        DateTime startTime,
        DateTime endTime,
        TimeSpan duration,
        decimal executionTime,
        string output,
        string? errorMessage,
        string? stackTrace)
    {
        var testCase = test.TestCase;
        var testMethod = testCase.TestMethod;
        var testClass = testMethod.TestClass;

        // Ensure environment info is populated in the session (only once)
        var session = XpingContext.CurrentSession;
        if (session != null && string.IsNullOrEmpty(session.EnvironmentInfo.MachineName))
        {
            var detector = new Xping.Sdk.Core.Environment.EnvironmentDetector();
            var config = XpingContext.Configuration;
            var collectNetworkMetrics = config?.CollectNetworkMetrics ?? false;
            var apiEndpoint = config?.ApiEndpoint;
            session.EnvironmentInfo = detector.Detect(collectNetworkMetrics, apiEndpoint);
        }

        return new TestExecution
        {
            ExecutionId = Guid.NewGuid(),
            TestId = test.TestCase.UniqueID ?? Guid.NewGuid().ToString(),
            TestName = test.DisplayName,
            FullyQualifiedName = $"{testClass.Class.Name}.{testMethod.Method.Name}",
            Assembly = testClass.Class.Assembly.Name,
            Namespace = testClass.Class.Name.Contains('.')
                ? testClass.Class.Name.Substring(0, testClass.Class.Name.LastIndexOf('.'))
                : string.Empty,
            Outcome = outcome,
            Duration = duration,
            StartTimeUtc = startTime,
            EndTimeUtc = endTime,
            SessionId = session?.SessionId,
            Metadata = ExtractMetadata(test, output),
            ErrorMessage = errorMessage ?? string.Empty,
            StackTrace = stackTrace ?? string.Empty,
        };
    }

    private static TestMetadata ExtractMetadata(ITest test, string output)
    {
        var testCase = test.TestCase;
        var categories = new System.Collections.Generic.List<string>();
        var tags = new System.Collections.Generic.List<string> { "framework:xunit" };
        var customAttributes = new System.Collections.Generic.Dictionary<string, string>();

        // Extract traits as categories
        if (testCase.Traits != null)
        {
            foreach (var trait in testCase.Traits)
            {
                var key = trait.Key;
                foreach (var value in trait.Value)
                {
                    if (key.Equals("Category", StringComparison.OrdinalIgnoreCase))
                    {
                        categories.Add(value);
                    }
                    else
                    {
                        tags.Add($"{key}:{value}");
                    }
                }
            }
        }

        // Add test method parameters if present
        if (testCase.TestMethodArguments != null && testCase.TestMethodArguments.Length > 0)
        {
            var args = string.Join(", ", testCase.TestMethodArguments.Select(a => a?.ToString() ?? "null"));
            customAttributes.Add("Arguments", args);
            tags.Add("type:theory");
        }
        else
        {
            tags.Add("type:fact");
        }

        // Add source file info if available
        if (testCase.SourceInformation != null)
        {
            if (!string.IsNullOrEmpty(testCase.SourceInformation.FileName))
            {
                customAttributes.Add("SourceFile", testCase.SourceInformation.FileName!);
            }

            if (testCase.SourceInformation.LineNumber.HasValue)
            {
                customAttributes.Add("SourceLine", testCase.SourceInformation.LineNumber.Value.ToString());
            }
        }

        // Add test output if present
        if (!string.IsNullOrEmpty(output))
        {
            customAttributes.Add("Output", output);
        }

        var metadata = new TestMetadata
        {
            Categories = categories,
            Tags = tags,
            Description = test.DisplayName,
        };

        foreach (var kvp in customAttributes)
        {
            metadata.CustomAttributes[kvp.Key] = kvp.Value;
        }

        return metadata;
    }

    private static TimeSpan CalculateDuration(long startTimestamp, long endTimestamp)
    {
        var elapsedTicks = endTimestamp - startTimestamp;
        return TimeSpan.FromTicks((long)(elapsedTicks * TimeSpan.TicksPerSecond / Stopwatch.Frequency));
    }

    private static string GetTestKey(ITest test)
    {
        return test.TestCase.UniqueID ?? test.DisplayName;
    }

    private sealed class TestExecutionData
    {
        public ITest Test { get; set; } = null!;
        public DateTime StartTime { get; set; }
        public long StartTimestamp { get; set; }
    }
}

#pragma warning restore CA1305
