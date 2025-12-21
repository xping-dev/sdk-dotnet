/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.XUnit;

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using Xunit.Abstractions;
using Xping.Sdk.Core.Collection;
using Xping.Sdk.Core.Models;
using Xping.Sdk.XUnit.Retry;

#pragma warning disable CA1305 // Specify IFormatProvider for culture-aware ToString conversions

/// <summary>
/// Message sink that intercepts xUnit test execution messages and records them to Xping.
/// Tracks test start/end times and outcomes in a thread-safe manner.
/// </summary>
public sealed class XpingMessageSink : IMessageSink
{
    private readonly IMessageSink _innerSink;
    private readonly ConcurrentDictionary<string, TestExecutionData> _testData;
    private readonly ExecutionTracker _executionTracker;
    private readonly ConcurrentDictionary<string, int> _activeCollections;
    private readonly XUnitRetryDetector _retryDetector;

    /// <summary>
    /// Initializes a new instance of the <see cref="XpingMessageSink"/> class.
    /// </summary>
    /// <param name="innerSink">The inner message sink to forward messages to.</param>
    public XpingMessageSink(IMessageSink innerSink)
    {
        _innerSink = innerSink ?? throw new ArgumentNullException(nameof(innerSink));
        _testData = new ConcurrentDictionary<string, TestExecutionData>();
        _executionTracker = new ExecutionTracker();
        _activeCollections = new ConcurrentDictionary<string, int>();
        _retryDetector = new XUnitRetryDetector();
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
        var collectionName = testStarting.Test.TestCase.TestMethod.TestClass.TestCollection.DisplayName;

        // Track active collections for parallelization detection
        _activeCollections.AddOrUpdate(collectionName, 1, (_, count) => count + 1);

        var data = new TestExecutionData
        {
            Test = testStarting.Test,
            StartTime = DateTime.UtcNow,
            StartTimestamp = Stopwatch.GetTimestamp(),
            CollectionName = collectionName,
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

        this.RecordTestExecution(
            test: data.Test,
            outcome: TestOutcome.Passed,
            startTime: data.StartTime,
            endTime: endTime,
            duration: duration,
            executionTime: testPassed.ExecutionTime,
            output: testPassed.Output,
            exceptionType: null,
            errorMessage: null,
            stackTrace: null,
            collectionName: data.CollectionName,
            executionTracker: _executionTracker);
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

        // Extract exception type - XUnit provides array of exception types
        var exceptionType = testFailed.ExceptionTypes?.FirstOrDefault();

        this.RecordTestExecution(
            test: data.Test,
            outcome: TestOutcome.Failed,
            startTime: data.StartTime,
            endTime: endTime,
            duration: duration,
            executionTime: testFailed.ExecutionTime,
            output: testFailed.Output,
            exceptionType: exceptionType,
            errorMessage: string.Join(Environment.NewLine, testFailed.Messages),
            stackTrace: string.Join(Environment.NewLine, testFailed.StackTraces),
            collectionName: data.CollectionName,
            executionTracker: _executionTracker);
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

        this.RecordTestExecution(
            test: data.Test,
            outcome: TestOutcome.Skipped,
            startTime: data.StartTime,
            endTime: endTime,
            duration: duration,
            executionTime: 0,
            output: string.Empty,
            exceptionType: null,
            errorMessage: $"Test skipped: {testSkipped.Reason}",
            stackTrace: null,
            collectionName: data.CollectionName,
            executionTracker: _executionTracker
        );
    }

    private void HandleTestAssemblyFinished()
    {
        // Flush all recorded test data when assembly finishes executing
        // This ensures data is uploaded, even when using VSTest adapter
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
        finally
        {
            ValueTask result = XpingContext.DisposeAsync();
            result.GetAwaiter().GetResult();
        }
    }

    private void RecordTestExecution(
        ITest test,
        TestOutcome outcome,
        DateTime startTime,
        DateTime endTime,
        TimeSpan duration,
        decimal executionTime,
        string output,
        string? exceptionType,
        string? errorMessage,
        string? stackTrace,
        string collectionName,
        ExecutionTracker executionTracker)
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
                exceptionType,
                errorMessage,
                stackTrace,
                collectionName,
                executionTracker);

            XpingContext.RecordTest(execution);
        }
        catch
        {
            // Swallow exceptions to avoid interfering with test execution
        }
    }

    private TestExecution CreateTestExecution(
        ITest test,
        TestOutcome outcome,
        DateTime startTime,
        DateTime endTime,
        TimeSpan duration,
        decimal executionTime,
        string output,
        string? exceptionType,
        string? errorMessage,
        string? stackTrace,
        string collectionName,
        ExecutionTracker executionTracker)
    {
        var testCase = test.TestCase;
        var testMethod = testCase.TestMethod;
        var testClass = testMethod.TestClass;

        // Generate stable test identity
        var fullyQualifiedName = $"{testClass.Class.Name}.{testMethod.Method.Name}";
        var assemblyName = testClass.Class.Assembly.Name;
        var parameters = testCase.TestMethodArguments;
        var displayName = test.DisplayName;
        var sourceFile = testCase.SourceInformation?.FileName;
        var sourceLine = testCase.SourceInformation?.LineNumber;

        var identity = TestIdentityGenerator.Generate(
            fullyQualifiedName,
            assemblyName,
            parameters,
            displayName,
            sourceFile,
            sourceLine);

        // Create execution context using collection name as worker ID
        var workerId = collectionName;
        var context = executionTracker.CreateContext(workerId, collectionName);

        // Detect retry metadata
        var retryMetadata = _retryDetector.DetectRetryMetadata(test);
        if (retryMetadata != null)
        {
            // Update attempt number with current value
            retryMetadata.AttemptNumber = _retryDetector.GetCurrentAttemptNumber(test);

            // If test passed and attempt > 1, it passed on retry
            if (outcome == TestOutcome.Passed && retryMetadata.AttemptNumber > 1)
            {
                retryMetadata.PassedOnRetry = true;
            }
        }

        var execution = new TestExecution
        {
            ExecutionId = Guid.NewGuid(),
            Identity = identity,
            TestName = test.DisplayName,
            Outcome = outcome,
            Duration = duration,
            StartTimeUtc = startTime,
            EndTimeUtc = endTime,
            SessionContext = XpingContext.CurrentSession,
            Metadata = ExtractMetadata(test, output),
            ExceptionType = exceptionType,
            ErrorMessage = errorMessage ?? string.Empty,
            StackTrace = stackTrace ?? string.Empty,
            ErrorMessageHash = TestIdentityGenerator.GenerateErrorMessageHash(errorMessage),
            StackTraceHash = TestIdentityGenerator.GenerateStackTraceHash(stackTrace),
            Context = context,
            Retry = retryMetadata
        };

        // Record test completion for tracking as previous test
        executionTracker.RecordTestCompletion(workerId, identity.TestId, test.DisplayName, outcome);

        return execution;
    }

    private static TestMetadata ExtractMetadata(ITest test, string output)
    {
        var testCase = test.TestCase;
        var categories = new List<string>();
        var tags = new List<string> { "framework:xunit" };
        var customAttributes = new Dictionary<string, string>();

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
        public string CollectionName { get; set; } = string.Empty;
    }
}

#pragma warning restore CA1305
