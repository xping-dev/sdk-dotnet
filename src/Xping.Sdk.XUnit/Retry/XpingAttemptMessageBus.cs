/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Diagnostics;
using Xping.Sdk.Core.Models.Executions;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Xping.Sdk.XUnit.Retry;

/// <summary>
/// Message bus for a single retry attempt: it records the attempt's outcome to Xping and forwards every
/// message, unchanged, to the retry library's bus.
/// </summary>
/// <remarks>
/// One instance is created per attempt, so the attempt number is carried by the bus itself rather than
/// inferred from ambient state — which would be unreliable, because xUnit's <see cref="MessageBus"/>
/// dispatches to sinks on its own thread, well after the attempt has moved on.
/// Forwarding is unconditional: whether the attempt is ultimately reported or discarded stays entirely
/// the retry library's decision.
/// </remarks>
internal sealed class XpingAttemptMessageBus(
    IMessageBus inner,
    XpingMessageSink sink,
    int attemptNumber,
    IReadOnlyCollection<string> skipOnExceptionFullNames) : IMessageBus
{
    private DateTime _startTime = DateTime.UtcNow;
    private long _startTimestamp = Stopwatch.GetTimestamp();

    /// <inheritdoc/>
    public bool QueueMessage(IMessageSinkMessage message)
    {
        try
        {
            Observe(message);
        }
        catch
        {
            // Recording must never interfere with test execution.
        }

        return inner.QueueMessage(message);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // The inner bus is owned by the retry library, which disposes it itself.
    }

    private void Observe(IMessageSinkMessage message)
    {
        switch (message)
        {
            case ITestStarting:
                _startTime = DateTime.UtcNow;
                _startTimestamp = Stopwatch.GetTimestamp();
                break;

            case ITestPassed testPassed:
                Record(
                    testPassed.Test,
                    TestOutcome.Passed,
                    XpingMessageSink.CalculateDuration(testPassed.ExecutionTime),
                    testPassed.Output,
                    exceptionType: null,
                    errorMessage: null,
                    stackTrace: null);
                break;

            case ITestFailed testFailed:
                RecordFailure(testFailed);
                break;

            case ITestSkipped testSkipped:
                Record(
                    testSkipped.Test,
                    TestOutcome.Skipped,
                    // xUnit hardcodes ExecutionTime to 0 for skipped tests, so measure it here instead.
                    XpingMessageSink.CalculateDuration(_startTimestamp, Stopwatch.GetTimestamp()),
                    output: string.Empty,
                    exceptionType: null,
                    errorMessage: $"Test skipped: {testSkipped.Reason}",
                    stackTrace: null);
                break;
        }
    }

    private void RecordFailure(ITestFailed testFailed)
    {
        string? exceptionType = testFailed.ExceptionTypes?.FirstOrDefault();

        // A retry library may present a failure caused by one of its skip-on exception types as a skip
        // (xRetry does, via the transformer inside its own bus, which runs after this one). Recording
        // the failure verbatim would contradict the outcome xUnit ultimately reports for the test.
        TestOutcome outcome = IsSkipOnException(exceptionType) ? TestOutcome.Skipped : TestOutcome.Failed;

        Record(
            testFailed.Test,
            outcome,
            XpingMessageSink.CalculateDuration(testFailed.ExecutionTime),
            testFailed.Output,
            exceptionType,
            errorMessage: string.Join(Environment.NewLine, testFailed.Messages ?? []),
            stackTrace: string.Join(Environment.NewLine, testFailed.StackTraces ?? []));
    }

    private bool IsSkipOnException(string? exceptionType) =>
        exceptionType != null &&
        skipOnExceptionFullNames.Contains(exceptionType, StringComparer.Ordinal);

    private void Record(
        ITest test,
        TestOutcome outcome,
        TimeSpan duration,
        string output,
        string? exceptionType,
        string? errorMessage,
        string? stackTrace) =>
        sink.RecordAttempt(
            test,
            outcome,
            _startTime,
            duration,
            output,
            exceptionType,
            errorMessage,
            stackTrace,
            attemptNumber);
}
