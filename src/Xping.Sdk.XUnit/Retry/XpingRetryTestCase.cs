/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Reflection;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Xping.Sdk.XUnit.Retry;

/// <summary>
/// Decorates a retry-library test case so that Xping observes every attempt, including the ones the
/// library discards.
/// </summary>
/// <remarks>
/// <para>
/// Every member is delegated to the wrapped test case, so test identity, traits, collection grouping and
/// runner correlation are unchanged. Only <see cref="RunAsync"/> differs: it hands the retry library its
/// own runner loop back (see <see cref="RetryTestCaseHook"/>) while supplying the per-attempt delegate,
/// which is what lets <see cref="XpingAttemptMessageBus"/> sit inside the loop.
/// </para>
/// <para>
/// The decorator, not the wrapped case, is passed to the per-attempt runner, so every message produced
/// carries the <see cref="IXpingManagedTestCase"/> marker and <see cref="XpingMessageSink"/> knows those
/// attempts are already recorded.
/// </para>
/// </remarks>
internal sealed class XpingRetryTestCase : IXunitTestCase, IXpingManagedTestCase
{
    private readonly IXunitTestCase _inner;
    private readonly XpingMessageSink _sink;
    private readonly MethodInfo _hook;
    private readonly IReadOnlyCollection<string> _skipOnExceptionFullNames;

    private XpingRetryTestCase(
        IXunitTestCase inner,
        XpingMessageSink sink,
        MethodInfo hook,
        IReadOnlyCollection<string> skipOnExceptionFullNames)
    {
        _inner = inner;
        _sink = sink;
        _hook = hook;
        _skipOnExceptionFullNames = skipOnExceptionFullNames;
    }

    /// <summary>
    /// Wraps a test case when its retry library exposes a hook Xping can observe from the inside.
    /// </summary>
    /// <param name="testCase">The test case discovered by xUnit.</param>
    /// <param name="sink">The sink that records the observed attempts.</param>
    /// <returns>
    /// The wrapped test case, or null when the case is not retryable or its library exposes no
    /// compatible hook — in which case the caller must run the original case untouched.
    /// </returns>
    internal static XpingRetryTestCase? TryWrap(IXunitTestCase testCase, XpingMessageSink sink)
    {
        try
        {
            MethodInfo? hook = RetryTestCaseHook.Resolve(testCase);
            if (hook == null || !RetryTestCaseHook.CanRun(hook, testCase))
            {
                return null;
            }

            return new XpingRetryTestCase(testCase, sink, hook, ReadSkipOnExceptionFullNames(testCase));
        }
        catch
        {
            // Never let retry observation prevent a test from running.
            return null;
        }
    }

    /// <inheritdoc/>
    public Task<RunSummary> RunAsync(
        IMessageSink diagnosticMessageSink,
        IMessageBus messageBus,
        object[] constructorArguments,
        ExceptionAggregator aggregator,
        CancellationTokenSource cancellationTokenSource)
    {
        int attemptNumber = 0;

        Task<RunSummary> RunSingleAttempt(IMessageBus attemptSink)
        {
            XpingAttemptMessageBus attemptBus = new(
                attemptSink,
                _sink,
                Interlocked.Increment(ref attemptNumber),
                _skipOnExceptionFullNames);

            // Mirrors the runners the retry library would have constructed for a single attempt, with
            // this decorator in place of the wrapped case so its messages are identifiable downstream.
            return RequiresTheoryRunner(_inner)
                ? new XunitTheoryTestCaseRunner(
                        this,
                        DisplayName,
                        SkipReason,
                        constructorArguments,
                        diagnosticMessageSink,
                        attemptBus,
                        aggregator,
                        cancellationTokenSource)
                    .RunAsync()
                : new XunitTestCaseRunner(
                        this,
                        DisplayName,
                        SkipReason,
                        constructorArguments,
                        TestMethodArguments,
                        attemptBus,
                        aggregator,
                        cancellationTokenSource)
                    .RunAsync();
        }

        try
        {
            object? result = _hook.Invoke(
                null,
                [
                    _inner,
                    diagnosticMessageSink,
                    messageBus,
                    cancellationTokenSource,
                    (Func<IMessageBus, Task<RunSummary>>)RunSingleAttempt
                ]);

            if (result is Task<RunSummary> runSummary)
            {
                return runSummary;
            }
        }
        catch (Exception)
        {
            // The hook is an async method, so a failure escaping the invocation itself is an argument
            // or binding failure raised before any attempt ran. Falling back to the wrapped case is
            // therefore safe: it cannot re-run a test that already executed.
        }

        return _inner.RunAsync(
            diagnosticMessageSink,
            messageBus,
            constructorArguments,
            aggregator,
            cancellationTokenSource);
    }

    /// <summary>
    /// Determines whether an attempt must be run by the theory runner.
    /// </summary>
    /// <remarks>
    /// A test case with arguments is a single pre-enumerated theory row and runs on the standard runner.
    /// A case without arguments whose method carries data attributes still has its data to enumerate,
    /// which only the theory runner does.
    /// </remarks>
    private static bool RequiresTheoryRunner(IXunitTestCase testCase)
    {
        if (testCase.TestMethodArguments != null)
        {
            return false;
        }

        if (testCase is XunitTheoryTestCase)
        {
            return true;
        }

        try
        {
            return testCase.TestMethod?.Method?.GetCustomAttributes(typeof(DataAttribute)).Any() == true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Reads the exception types the retry library reports as skips rather than failures.
    /// </summary>
    private static string[] ReadSkipOnExceptionFullNames(IXunitTestCase testCase)
    {
        try
        {
            PropertyInfo? property = testCase.GetType().GetProperty("SkipOnExceptionFullNames");
            if (property?.GetValue(testCase) is string[] fullNames)
            {
                return fullNames;
            }
        }
        catch
        {
            // Treated as "no skip-on exceptions configured".
        }

        return [];
    }

    // --------------------------------------------------------------------------------------------
    // Delegated members
    // --------------------------------------------------------------------------------------------

    /// <inheritdoc/>
    public string DisplayName => _inner.DisplayName;

    /// <inheritdoc/>
    public string? SkipReason => _inner.SkipReason;

    /// <inheritdoc/>
    public ISourceInformation SourceInformation
    {
        get => _inner.SourceInformation;
        set => _inner.SourceInformation = value;
    }

    /// <inheritdoc/>
    public ITestMethod TestMethod => _inner.TestMethod;

    /// <inheritdoc/>
    public object[] TestMethodArguments => _inner.TestMethodArguments;

    /// <inheritdoc/>
    public Dictionary<string, List<string>> Traits => _inner.Traits;

    /// <inheritdoc/>
    public string UniqueID => _inner.UniqueID;

    /// <inheritdoc/>
    public IMethodInfo Method => _inner.Method;

    /// <inheritdoc/>
    public Exception? InitializationException => _inner.InitializationException;

    /// <inheritdoc/>
    public int Timeout => _inner.Timeout;

    /// <inheritdoc/>
    public void Serialize(IXunitSerializationInfo info) => _inner.Serialize(info);

    /// <inheritdoc/>
    public void Deserialize(IXunitSerializationInfo info) => _inner.Deserialize(info);
}
