/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Tests.Report;

/// <summary>
/// Failure text copied verbatim out of recorded sessions.
/// </summary>
/// <remarks>
/// <para>
/// Normalisation is designed against these, not against invented strings. Every one of them came
/// out of <c>.xping/sessions/</c> in this repository, produced by a real run of the sample suites,
/// and they carry the details that made the rules necessary: the millisecond readings that differ
/// between two runs of the same failure, NUnit's leading indent and constraint block, the object
/// dump full of timestamps that xUnit's collection asserts print, and MSTest's silence.
/// </para>
/// <para>
/// A rule tested against a string someone imagined is a rule that works on strings someone
/// imagined.
/// </para>
/// </remarks>
internal static class RealFailureSamples
{
    /// <summary>xUnit <c>Assert.True</c> with a user message, first observed run.</summary>
    public const string XunitWatchdogFirstRun =
        "Watchdog (126 ms) fired before the simulated service responded (202 ms). This reproduces " +
        "flakiness caused by network timeouts, service-side back-pressure, or CPU contention that " +
        "shifts task-scheduling order.";

    /// <summary>The same xUnit failure on a later run — only the readings differ.</summary>
    public const string XunitWatchdogSecondRun =
        "Watchdog (109 ms) fired before the simulated service responded (189 ms). This reproduces " +
        "flakiness caused by network timeouts, service-side back-pressure, or CPU contention that " +
        "shifts task-scheduling order.";

    /// <summary>The stack trace recorded alongside <see cref="XunitWatchdogFirstRun"/>.</summary>
    public const string XunitWatchdogStackTrace =
        "   at Xunit.Assert.True(Nullable`1 condition, String userMessage) in /_/src/xunit.assert/Asserts/BooleanAsserts.cs:line 141\n" +
        "   at Xunit.Assert.True(Boolean condition, String userMessage) in /_/src/xunit.assert/Asserts/BooleanAsserts.cs:line 123\n" +
        "   at SampleApp.XUnit.SampleTests.FlakyTest_EnvironmentState_FailsBasedOnSystemState() in /Users/adrian/Dev/xping/sdk-dotnet/samples/SampleApp.XUnit/SampleTests.cs:line 72\n" +
        "   at Xunit.Sdk.TestInvoker`1.<>c__DisplayClass47_0.<<InvokeTestMethodAsync>b__1>d.MoveNext() in /_/src/xunit.execution/Sdk/Frameworks/Runners/TestInvoker.cs:line 259\n" +
        "--- End of stack trace from previous location ---\n" +
        "   at Xunit.Sdk.ExecutionTimer.AggregateAsync(Func`1 asyncAction) in /_/src/xunit.execution/Sdk/Frameworks/ExecutionTimer.cs:line 48\n" +
        "   at Xunit.Sdk.ExceptionAggregator.RunAsync(Func`1 code) in /_/src/xunit.core/Sdk/ExceptionAggregator.cs:line 90";

    /// <summary>xUnit <c>Assert.Empty</c>, whose message embeds a record dump.</summary>
    public const string XunitAssertEmpty =
        "Assert.Empty() Failure: Collection was not empty\n" +
        "Collection: [TestSession { EndedAt = 2026-08-10T20:08:30.8241590Z, EnvironmentInfo = " +
        "EnvironmentInfo { CustomProperties = [···], EnvironmentName = \"Local\", Framework = " +
        "\".NET\", IsCIEnvironment = False, MachineName = \"addydeck\", ··· }, Executions = " +
        "[TestExecution { Duration = 00:00:00.0028939, EndTimeUtc = 2026-08-10T20:08:29.3591150Z, " +
        "ErrorMessage = null, ErrorMessageHash = null, ExceptionType = null, ··· }] }]";

    /// <summary>An exception thrown by a test, as xUnit records it — the message alone.</summary>
    public const string XunitThrownException = "This is a test exception for tracking purposes.";

    /// <summary>The stack trace recorded alongside <see cref="XunitThrownException"/>.</summary>
    public const string XunitThrownExceptionStackTrace =
        "   at SampleApp.XUnit.SampleTests.ThrowingTestIsTracked() in /Users/adrian/Dev/xping/sdk-dotnet/samples/SampleApp.XUnit/SampleTests.cs:line 36\n" +
        "   at System.Reflection.MethodBaseInvoker.InterpretedInvoke_Method(Object obj, IntPtr* args)\n" +
        "   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)";

    /// <summary>NUnit <c>Assert.That</c> with a user message; the adapter records no type for it.</summary>
    public const string NUnitAssertThat =
        "  Watchdog (106 ms) fired before the simulated service responded (190 ms). This " +
        "reproduces flakiness caused by network timeouts, service-side back-pressure, or CPU " +
        "contention that shifts task-scheduling order.\n" +
        "Assert.That(winner == serviceCall, Is.True)\n" +
        "  Expected: True\n" +
        "  But was:  False\n";

    /// <summary>The stack trace recorded alongside <see cref="NUnitAssertThat"/>.</summary>
    public const string NUnitAssertThatStackTrace =
        "   at SampleApp.NUnit.SampleTests.FlakyTest_RandomFailure_FailsProbabilistically() in /Users/adrian/Dev/xping/sdk-dotnet/samples/SampleApp.NUnit/SampleTests.cs:line 83\n" +
        "   at NUnit.Framework.Internal.TaskAwaitAdapter.GenericAdapter`1.BlockUntilCompleted()\n" +
        "   at NUnit.Framework.Internal.MessagePumpStrategy.NoMessagePumpStrategy.WaitForCompletion(AwaitAdapter awaiter)\n" +
        "   at NUnit.Framework.Internal.AsyncToSyncAdapter.Await[TResult](Func`1 invoke)\n" +
        "   at NUnit.Framework.Internal.Commands.TestMethodCommand.Execute(TestExecutionContext context)\n";

    /// <summary>An exception thrown by a test, as NUnit records it — type prefixed into the message.</summary>
    public const string NUnitThrownException =
        "System.InvalidOperationException : This is a test exception for tracking purposes.";

    /// <summary>
    /// A stack trace made entirely of framework frames, as produced when a shared assertion helper
    /// fails before reaching any code under test.
    /// </summary>
    public const string FrameworkOnlyStackTrace =
        "   at Xunit.Assert.True(Boolean condition) in /_/src/xunit.assert/Asserts/BooleanAsserts.cs:line 123\n" +
        "   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)";
}
