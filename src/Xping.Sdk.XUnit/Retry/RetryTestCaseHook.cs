/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Collections.Concurrent;
using System.Reflection;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Xping.Sdk.XUnit.Retry;

/// <summary>
/// Binds — by reflection, without taking a dependency on the retry library — to a retry runner that
/// lets a caller supply the single-attempt delegate, and therefore observe every attempt.
/// </summary>
/// <remarks>
/// <para>
/// xUnit retry libraries run their attempts behind a message bus of their own that discards the
/// messages of any attempt they intend to retry. A message sink such as <see cref="XpingMessageSink"/>
/// sits outside that bus and can only ever see the one attempt the library chooses to flush.
/// </para>
/// <para>
/// xRetry exposes <c>public static Task&lt;RunSummary&gt; RetryTestCaseRunner.RunAsync(IRetryableTestCase,
/// IMessageSink, IMessageBus, CancellationTokenSource, Func&lt;IMessageBus, Task&lt;RunSummary&gt;&gt;)</c>
/// explicitly so other xUnit extensions can integrate with it. Supplying the final parameter puts Xping
/// between the library's blocking bus and the actual test runner, which is the only place every attempt
/// is visible. The library keeps full control of the retry count, delays, skip-on-exception handling and
/// flushing, so test behavior is unchanged.
/// </para>
/// <para>
/// The signature is validated before use and every failure path returns <see langword="null"/>, which
/// leaves the test case unwrapped and behaving exactly as it does without Xping.
/// </para>
/// </remarks>
internal static class RetryTestCaseHook
{
    private const string RunnerTypeName = "xRetry.RetryTestCaseRunner";
    private const string RunAsyncMethodName = "RunAsync";

    // Keyed by the assembly that declares the test case, so the reflection cost is paid once per
    // retry library rather than once per test case.
    private static readonly ConcurrentDictionary<Assembly, MethodInfo?> _hooks = new();

    /// <summary>
    /// Resolves the retry runner hook for the library that declares the given test case.
    /// </summary>
    /// <param name="testCase">The test case discovered by the retry library.</param>
    /// <returns>The runner method, or null when the library exposes no compatible hook.</returns>
    internal static MethodInfo? Resolve(IXunitTestCase testCase) =>
        _hooks.GetOrAdd(testCase.GetType().Assembly, ResolveCore);

    /// <summary>
    /// Determines whether the given test case is one the resolved hook can run.
    /// </summary>
    /// <remarks>
    /// The hook's first parameter is the library's own retryable-test-case contract
    /// (<c>xRetry.IRetryableTestCase</c>), so an assignability check is both the precise test for
    /// "this case carries retry configuration" and the guarantee that the invocation will bind.
    /// </remarks>
    internal static bool CanRun(MethodInfo hook, IXunitTestCase testCase) =>
        hook.GetParameters()[0].ParameterType.IsInstanceOfType(testCase);

    private static MethodInfo? ResolveCore(Assembly assembly)
    {
        try
        {
            Type? runnerType = assembly.GetType(RunnerTypeName, throwOnError: false);
            if (runnerType == null)
            {
                return null;
            }

            foreach (MethodInfo method in runnerType.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (method.Name == RunAsyncMethodName && HasExpectedSignature(method))
                {
                    return method;
                }
            }

            return null;
        }
        catch
        {
            // A library that cannot be inspected is simply not hooked.
            return null;
        }
    }

    private static bool HasExpectedSignature(MethodInfo method)
    {
        if (method.ReturnType != typeof(Task<RunSummary>))
        {
            return false;
        }

        ParameterInfo[] parameters = method.GetParameters();

        return parameters.Length == 5 &&
               typeof(IXunitTestCase).IsAssignableFrom(parameters[0].ParameterType) &&
               parameters[1].ParameterType == typeof(IMessageSink) &&
               parameters[2].ParameterType == typeof(IMessageBus) &&
               parameters[3].ParameterType == typeof(CancellationTokenSource) &&
               parameters[4].ParameterType == typeof(Func<IMessageBus, Task<RunSummary>>);
    }
}
