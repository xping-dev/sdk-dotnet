/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Xping.Sdk.Core.Models.Builders;
using Xunit.Abstractions;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.Collector;
using Xping.Sdk.Core.Services.Identity;
using Xping.Sdk.Core.Services.Retry;
using Xping.Sdk.Shared;

namespace Xping.Sdk.XUnit;

/// <summary>
/// Message sink that intercepts xUnit test execution messages and records them to Xping.
/// Tracks test start/end times and outcomes in a thread-safe manner.
/// </summary>
public sealed class XpingMessageSink(
    IMessageSink innerSink,
    IExecutionTracker executionTracker,
    IRetryDetector<ITest> retryDetector,
    ITestIdentityGenerator identityGenerator,
    ILogger<XpingMessageSink> logger) : IMessageSink
{
    private readonly IMessageSink _innerSink = innerSink.RequireNotNull();
    private readonly IExecutionTracker _executionTracker = executionTracker.RequireNotNull();
    private readonly IRetryDetector<ITest> _retryDetector = retryDetector.RequireNotNull();
    private readonly ITestIdentityGenerator _identityGenerator = identityGenerator.RequireNotNull();
    private readonly ILogger<XpingMessageSink> _logger = logger.RequireNotNull();

    private readonly ConcurrentDictionary<string, TestExecutionData> _testData = new();
    private readonly ConcurrentDictionary<string, int> _activeCollections = new();

    /// <summary>
    /// Handles incoming messages from xUnit test execution.
    /// </summary>
    /// <param name="message">The message to process.</param>
    /// <returns>True if the message was processed successfully.</returns>
    bool IMessageSink.OnMessage(IMessageSinkMessage message)
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

        // Forward to the inner sink
        return _innerSink.OnMessage(message);
    }

    private void HandleTestStarting(ITestStarting testStarting)
    {
        string testKey = GetTestKey(testStarting.Test);
        string? collectionName = testStarting.Test.TestCase.TestMethod.TestClass.TestCollection.DisplayName;

        // Track active collections for parallelization detection
        _activeCollections.AddOrUpdate(collectionName, 1, (_, count) => count + 1);

        TestExecutionData data = new()
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
        string testKey = GetTestKey(testPassed.Test);
        if (!_testData.TryRemove(testKey, out TestExecutionData? data))
        {
            return;
        }

        DateTime endTime = DateTime.UtcNow;
        TimeSpan duration = CalculateDuration(testPassed.ExecutionTime);

        RecordTestExecution(
            test: data.Test,
            outcome: TestOutcome.Passed,
            startTime: data.StartTime,
            endTime: endTime,
            duration: duration,
            output: testPassed.Output,
            exceptionType: null,
            errorMessage: null,
            stackTrace: null,
            collectionName: data.CollectionName);
    }

    private void HandleTestFailed(ITestFailed testFailed)
    {
        string testKey = GetTestKey(testFailed.Test);
        if (!_testData.TryRemove(testKey, out TestExecutionData? data))
        {
            return;
        }

        DateTime endTime = DateTime.UtcNow;
        TimeSpan duration = CalculateDuration(testFailed.ExecutionTime);

        // Extract exception type - XUnit provides an array of exception types
        string? exceptionType = testFailed.ExceptionTypes?.FirstOrDefault();

        RecordTestExecution(
            test: data.Test,
            outcome: TestOutcome.Failed,
            startTime: data.StartTime,
            endTime: endTime,
            duration: duration,
            output: testFailed.Output,
            exceptionType: exceptionType,
            errorMessage: string.Join(Environment.NewLine, testFailed.Messages),
            stackTrace: string.Join(Environment.NewLine, testFailed.StackTraces),
            collectionName: data.CollectionName);
    }

    private void HandleTestSkipped(ITestSkipped testSkipped)
    {
        string testKey = GetTestKey(testSkipped.Test);
        if (!_testData.TryRemove(testKey, out TestExecutionData? data))
        {
            return;
        }

        DateTime endTime = DateTime.UtcNow;
        long endTimestamp = Stopwatch.GetTimestamp();
        TimeSpan duration = CalculateDuration(data.StartTimestamp, endTimestamp);

        RecordTestExecution(
            test: data.Test,
            outcome: TestOutcome.Skipped,
            startTime: data.StartTime,
            endTime: endTime,
            duration: duration,
            output: string.Empty,
            exceptionType: null,
            errorMessage: $"Test skipped: {testSkipped.Reason}",
            stackTrace: null,
            collectionName: data.CollectionName);
    }

    private void HandleTestAssemblyFinished()
    {
        // Finalize the session when the assembly finishes. FinalizeAsync includes an
        // internal flush and is idempotent, so it is safe to call here even if
        // XpingTestFramework.Dispose later calls DisposeAsync (which also finalizes).
        // This path is the primary safeguard for VSTest adapter runs, where Dispose
        // is not reliably called on custom frameworks.
        try
        {
            XpingContext.FinalizeAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finalizing Xping session on assembly finished");
        }
    }

    private void RecordTestExecution(
        ITest test,
        TestOutcome outcome,
        DateTime startTime,
        DateTime endTime,
        TimeSpan duration,
        string output,
        string? exceptionType,
        string? errorMessage,
        string? stackTrace,
        string collectionName)
    {
        try
        {
            TestExecution execution = CreateTestExecution(
                test,
                outcome,
                startTime,
                endTime,
                duration,
                output,
                exceptionType,
                errorMessage,
                stackTrace,
                collectionName);

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
        string output,
        string? exceptionType,
        string? errorMessage,
        string? stackTrace,
        string collectionName)
    {
        ITestCase? testCase = test.TestCase;
        ITestMethod? testMethod = testCase.TestMethod;
        ITestClass? testClass = testMethod.TestClass;

        // Generate stable test identity
        string fullyQualifiedName = $"{testClass.Class.Name}.{testMethod.Method.Name}";
        string? assemblyName = testClass.Class.Assembly.Name;
        object[]? parameters = testCase.TestMethodArguments;
        string? displayName = test.DisplayName;
        string? sourceFile = testCase.SourceInformation?.FileName;
        int? sourceLine = testCase.SourceInformation?.LineNumber;

        TestIdentity identity = _identityGenerator.Generate(
            fullyQualifiedName,
            assemblyName,
            parameters,
            displayName,
            sourceFile,
            sourceLine);

        // Extract test metadata
        TestMetadata metadata = ExtractMetadata(test, output);
        // Detect retry metadata first so the attempt number is available when claiming a position.
        RetryMetadata? retryMetadata = _retryDetector.DetectRetryMetadata(test, outcome);
        // Create execution context using collection name as worker ID.
        // Pass the attempt number so retried executions reuse the position of the first attempt.
        TestOrchestrationRecord orchestrationRecord = _executionTracker.CreateExecutionContext(
            workerId: collectionName, attemptNumber: retryMetadata?.AttemptNumber ?? 1);

        TestExecution testExecution = new TestExecutionBuilder()
            .WithExecutionId(Guid.NewGuid())
            .WithIdentity(identity)
            .WithTestName(test.DisplayName)
            .WithOutcome(outcome)
            .WithDuration(duration)
            .WithStartTime(startTime)
            .WithEndTime(endTime)
            .WithMetadata(metadata)
            .WithException(exceptionType, errorMessage, stackTrace)
            .WithErrorMessageHash(_identityGenerator.GenerateErrorMessageHash(errorMessage))
            .WithStackTraceHash(_identityGenerator.GenerateStackTraceHash(stackTrace))
            .WithTestOrchestrationRecord(orchestrationRecord)
            .WithRetry(retryMetadata)
            .Build();

        // Record test completion for tracking as previous test
        _executionTracker.RecordTestCompletion(workerId: collectionName, identity.TestFingerprint, test.DisplayName, outcome);

        return testExecution;
    }

    private static TestMetadata ExtractMetadata(ITest test, string output)
    {
        TestMetadataBuilder builder = new();

        ITestCase? testCase = test.TestCase;
        List<string> categories = [];
        List<string> tags = ["framework:xunit"];
        Dictionary<string, string> customAttributes = [];

        // Extract traits as categories
        if (testCase.Traits != null)
        {
            foreach (KeyValuePair<string, List<string>> trait in testCase.Traits)
            {
                string? key = trait.Key;
                foreach (string? value in trait.Value)
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
        if (testCase.TestMethodArguments is { Length: > 0 })
        {
            string args = string.Join(", ", testCase.TestMethodArguments.Select(a => a?.ToString() ?? "null"));
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
                string fileName = testCase.SourceInformation.FileName;
                customAttributes.Add("SourceFile", fileName);
            }

            if (testCase.SourceInformation.LineNumber.HasValue)
            {
                string lineNumber = testCase.SourceInformation.LineNumber.Value.ToString(CultureInfo.InvariantCulture);
                customAttributes.Add("SourceLine", lineNumber);
            }
        }

        // Add test output if present
        if (!string.IsNullOrEmpty(output))
        {
            customAttributes.Add("Output", output);
        }

        TestMetadata metadata = builder
            .AddCategories(categories)
            .AddTags(tags)
            .WithDescription(test.DisplayName)
            .AddCustomAttributes(customAttributes)
            .Build();

        return metadata;
    }

    private static TimeSpan CalculateDuration(long startTimestamp, long endTimestamp)
    {
        long elapsedTicks = endTimestamp - startTimestamp;
        return TimeSpan.FromTicks(elapsedTicks * TimeSpan.TicksPerSecond / Stopwatch.Frequency);
    }

    private static TimeSpan CalculateDuration(decimal executionTime)
    {
        // xUnit ExecutionTime is in seconds (decimal); convert to TimeSpan via ticks for precision
        long ticks = (long)(executionTime * TimeSpan.TicksPerSecond);
        return TimeSpan.FromTicks(ticks);
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
