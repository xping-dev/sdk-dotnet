/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xping.Sdk.Core.Attributes;
using Xping.Sdk.Core.Exceptions;
using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.Diagnostics;
using Xping.Sdk.MSTest.Retry;

namespace Xping.Sdk.MSTest;

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

    // Captured in TestInitialize rather than read again in TestCleanup: for an async test method the
    // cleanup continuation can resume on a different pool thread, which would split the test's start
    // and end — and its ordering chain — across two worker keys.
    private string? _workerKey;

    // Lifecycle members of a test class, resolved once per type. Cleanup runs for every test, and
    // walking the same class's hierarchy on each of them would put reflection in the path of the
    // whole suite.
    private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, FailureSite>>
        _lifecycleMembers = new();

    // Resolved once on first XpingTestInitialize() call and reused for every cleanup on this instance.
    // The DI singletons are the same each time, so a single resolution per instance is enough.
    private XpingBaseServices _services = null!;

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
        // Self-initialize if XpingAssemblyInitialize was not included in the test project.
        if (!XpingContext.IsInitialized)
            XpingContext.Initialize();

        // Resolve and cache services once per test class instance. GetBaseServices()
        // materializes the Lazy<XpingContext> on the first call (building the DI host), so
        // later calls on the same instance are a no-op field read.
        try
        {
            _services ??= XpingContext.GetBaseServices();
        }
        catch (XpingConfigurationException ex)
        {
            // Re-throwing from TestInitialize only fails the current test; MSTest still
            // attempts every subsequent test. FailFast aborts the process immediately, which
            // is the correct behavior for strict mode where observability must be guaranteed.
            Environment.FailFast($"[Xping] Strict mode configuration error: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Xping] SDK initialization failed: {ex.Message}");
        }

        _startTime = DateTime.UtcNow;
        _startTimestamp = Stopwatch.GetTimestamp();
        _workerKey = Environment.CurrentManagedThreadId.ToString(CultureInfo.InvariantCulture);

        try
        {
            // Mark the test in flight so the tests it overlaps with can be measured. The matching end
            // runs in XpingTestCleanup's finally block.
            _services.ExecutionTracker.RecordTestStart(_workerKey);
        }
        catch
        {
            // Swallow exceptions to avoid interfering with test execution: _services is unset when
            // the SDK failed to initialize above.
        }
    }

    /// <summary>
    /// Cleans up after each test and records the test execution.
    /// </summary>
    [TestCleanup]
    public void XpingTestCleanup()
    {
        try
        {
            if (TestContext == null)
                return;

            var endTimestamp = Stopwatch.GetTimestamp();
            var endTime = DateTime.UtcNow;

            var elapsedTicks = endTimestamp - _startTimestamp;
            var duration = TimeSpan.FromTicks(elapsedTicks * TimeSpan.TicksPerSecond / Stopwatch.Frequency);

            var workerKey = _workerKey ?? Environment.CurrentManagedThreadId.ToString(CultureInfo.InvariantCulture);
            var className = TestContext.FullyQualifiedTestClassName ?? "Unknown";

            try
            {
                var execution = CreateTestExecution(_services, TestContext, _startTime, endTime, duration, workerKey, className);
                XpingContext.RecordTest(execution);
            }
            catch
            {
                // Swallow exceptions to avoid interfering with test execution
            }
        }
        finally
        {
            if (_workerKey != null)
            {
                try
                {
                    // Release the in-flight slot even when recording failed, so later tests are not
                    // reported as having run concurrently with this one. Runs after the record above
                    // was built, so this test still counts itself.
                    _services.ExecutionTracker.RecordTestEnd(_workerKey);
                }
                catch
                {
                    // Swallow exceptions to avoid interfering with test execution
                }
            }
        }
    }

    private static TestExecution CreateTestExecution(
        XpingBaseServices services,
        TestContext context,
        DateTime startTime,
        DateTime endTime,
        TimeSpan duration,
        string workerKey,
        string className)
    {
        var outcome = ResolveOutcome(context);

        // Resolve the real test method once, so both the assembly name and the pinned fingerprint
        // come from actual reflection data instead of parsing the fully qualified class name.
        MethodInfo? testMethod = FindTestMethodForContext(context);

        // The assembly's simple name (e.g. "MyApp.Tests") — TestContext exposes no assembly
        // member directly, so resolve it from the resolved test class type. Uses ReflectedType,
        // not DeclaringType: FindTestMethodForContext searches inherited methods too, so for a
        // test method inherited from a base fixture in another assembly, DeclaringType would be
        // that base class rather than the actual test project. Falls back to the namespace-root
        // heuristic only when the type can't be resolved.
        var fullClassName = context.FullyQualifiedTestClassName ?? string.Empty;
        var assemblyName = testMethod?.ReflectedType?.Assembly.GetName().Name
            ?? ExtractAssemblyName(fullClassName);

        // Build the fully qualified test name
        var fullyQualifiedName = $"{context.FullyQualifiedTestClassName}.{context.TestName}";

        // Extract DataRow parameters if present (MSTest 3.7+)
        object[]? parameters = ExtractDataRowParameters(context);

        // Format test name with parameters to match NUnit and xUnit display format
        var testName = FormatTestNameWithParameters(context.TestName ?? "Unknown", parameters);

        // Read the pinned fingerprint from [XpingFingerprint] if present on the test method
        string? pinnedFingerprint = testMethod?.GetCustomAttribute<XpingFingerprintAttribute>(inherit: false)?.Fingerprint;

        // Read the declared timeout from the same MethodInfo, which is already resolved.
        (TimeSpan? timeoutBudget, TimeoutBudgetSource? timeoutBudgetSource) = ResolveTimeoutBudget(testMethod);

        // MSTest's TestContext carries no source information, so the declaration site comes from the
        // assembly's PDB, keyed by the MethodInfo already resolved above. It inherits that
        // resolution's limits: an overloaded test method matches by name alone.
        (string? sourceFile, int? sourceLineNumber) = SourceLocationLookup.Of(testMethod);

        // Generate stable test identity
        TestIdentity identity = services.IdentityGenerator.Generate(
            fullyQualifiedName,
            assemblyName,
            parameters,
            testName,
            sourceFile,
            sourceLineNumber,
            testFingerprint: pinnedFingerprint);

        var errorMessage = GetErrorMessage(context) ?? string.Empty;
        string? stackTrace = GetStackTrace(context);
        (string? configuredStackTrace, bool stackTraceOmitted) =
            ResolveStackTrace(outcome, stackTrace, services.CaptureStackTraces);

        // Resolved from the raw trace, before ResolveStackTrace nulls it for a user who opted out of
        // capture. The site is a classification rather than the trace itself, so it survives that
        // choice.
        (FailureSite? failureSite, string? failureSiteMember) =
            ResolveFailureSite(outcome, testMethod?.ReflectedType, stackTrace);

        // Detect retry metadata first so the attempt number is available when claiming a position.
        // The MSTest detector numbers attempts by counting the executions already recorded for this
        // test identity, which is why it needs the fingerprint resolved above.
        RetryMetadata? retryMetadata = services.RetryDetector is IMSTestRetryDetector retryDetector
            ? retryDetector.DetectRetryMetadata(context, outcome, identity.TestFingerprint)
            : services.RetryDetector.DetectRetryMetadata(context, outcome);

        // Create an execution context using ExecutionTracker.
        // Pass the attempt number so retried executions reuse the position of the first attempt.
        var orchestrationRecord = services.ExecutionTracker.CreateExecutionContext(
            workerKey, className, retryMetadata?.AttemptNumber ?? 1);

        TestMetadata metadata = ExtractMetadata(context);

        TestExecution execution = new TestExecutionBuilder()
            .WithExecutionId(Guid.NewGuid())
            .WithIdentity(identity)
            .WithTestName(testName)
            .WithOutcome(outcome)
            .WithDuration(duration)
            .WithStartTime(startTime)
            .WithEndTime(endTime)
            .WithMetadata(metadata)
            .WithException(GetExceptionType(context), errorMessage, configuredStackTrace)
            .WithFailureSite(failureSite, failureSiteMember)
            .WithErrorMessageHash(services.IdentityGenerator.GenerateErrorMessageHash(errorMessage))
            .WithStackTraceHash(services.IdentityGenerator.GenerateStackTraceHash(configuredStackTrace))
            .WithStackTraceOmitted(stackTraceOmitted)
            .WithTimeoutBudget(timeoutBudget, timeoutBudgetSource)
            .WithTestOrchestrationRecord(orchestrationRecord)
            .WithRetry(retryMetadata)
            .Build();

        // Record test completion for tracking as previous test
        services.ExecutionTracker.RecordTestCompletion(workerKey, identity.TestFingerprint, testName, outcome);

        return execution;
    }

    /// <summary>
    /// Determines where in the test lifecycle a failing execution failed.
    /// </summary>
    /// <param name="outcome">The outcome Xping resolved.</param>
    /// <param name="testClass">The resolved test class, or <see langword="null"/> when it could not be found.</param>
    /// <param name="stackTrace">The failing exception's stack trace, before any capture setting is applied.</param>
    /// <returns>The site and the member that failed, or two nulls when the test did not fail.</returns>
    /// <remarks>
    /// <para>
    /// MSTest exposes no site. <see cref="TestContext.CurrentTestOutcome"/> is <c>Failed</c> whether the
    /// body or a <c>[TestInitialize]</c> threw, and <see cref="TestContext.TestException"/> holds the
    /// exception the user's code raised with no wrapper and no message prefix around it. The stack
    /// trace is the only thing that differs between the two, so the class's own lifecycle methods are
    /// what it is matched against.
    /// </para>
    /// <para>
    /// Several sites are unreachable through this hook and are mapped anyway, since the cost is a
    /// dictionary entry and the alternative is a silent misclassification if MSTest changes: a
    /// <c>[TestCleanup]</c> that throws aborts the cleanup chain before this base class runs, and a
    /// failing <c>[ClassInitialize]</c> aborts the class before <c>[TestInitialize]</c> — in both cases
    /// no execution is recorded at all. See <c>docs/known-limitations.md</c>.
    /// </para>
    /// </remarks>
    internal static (FailureSite? Site, string? Member) ResolveFailureSite(
        TestOutcome outcome, Type? testClass, string? stackTrace)
    {
        if (!outcome.IsFailure())
        {
            return (null, null);
        }

        // A test the runner stopped was interrupted wherever it happened to be. The frame on top says
        // where the clock ran out, not what is broken.
        if (outcome == TestOutcome.Timeout)
        {
            return (FailureSite.Unknown, null);
        }

        if (testClass == null)
        {
            return (FailureSite.Unknown, null);
        }

        IReadOnlyDictionary<string, FailureSite> members =
            _lifecycleMembers.GetOrAdd(testClass, MapLifecycleMembers);

        string? frame = StackFrameLookup.FirstMatch(stackTrace, new List<string>(members.Keys));
        if (frame == null)
        {
            return (FailureSite.Unknown, null);
        }

        FailureSite site = members[frame];

        // The record already names the test; repeating it as the failing member would add a column
        // that never says anything new.
        return (site, site == FailureSite.TestBody ? null : StackFrameLookup.Shorten(frame));
    }

    private static IReadOnlyDictionary<string, FailureSite> MapLifecycleMembers(Type testClass)
    {
        var members = new Dictionary<string, FailureSite>(StringComparer.Ordinal);

        try
        {
            const BindingFlags flags =
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            // Walked rather than flattened: MSTest inherits a base class's [TestInitialize], and
            // FlattenHierarchy skips private members, so a non-public one would be missed.
            for (Type? type = testClass; type != null && type != typeof(object); type = type.BaseType)
            {
                foreach (MethodInfo method in type.GetMethods(flags))
                {
                    FailureSite? site = SiteOf(method);
                    if (site == null)
                    {
                        continue;
                    }

                    // A method overridden in a derived class appears twice; the derived one is seen
                    // first and is the one that runs.
                    string key = StackFrameLookup.Member(type.FullName, method.Name);
                    if (!members.ContainsKey(key))
                    {
                        members[key] = site.Value;
                    }
                }
            }
        }
        catch (Exception)
        {
            // A class whose members cannot be reflected leaves the site unresolved rather than
            // failing the test.
            return new Dictionary<string, FailureSite>(0);
        }

        return members;
    }

    private static FailureSite? SiteOf(MethodInfo method)
    {
        if (method.IsDefined(typeof(TestInitializeAttribute), inherit: true))
            return FailureSite.TestSetup;

        if (method.IsDefined(typeof(TestCleanupAttribute), inherit: true))
            return FailureSite.TestTeardown;

        if (method.IsDefined(typeof(ClassInitializeAttribute), inherit: true))
            return FailureSite.FixtureSetup;

        if (method.IsDefined(typeof(ClassCleanupAttribute), inherit: true))
            return FailureSite.FixtureTeardown;

        if (method.IsDefined(typeof(AssemblyInitializeAttribute), inherit: true))
            return FailureSite.AssemblySetup;

        if (method.IsDefined(typeof(AssemblyCleanupAttribute), inherit: true))
            return FailureSite.AssemblyTeardown;

        // Test methods are mapped too, so a body failure is recognised as such rather than falling
        // through to Unknown alongside the failures nothing could classify. [DataTestMethod] derives
        // from [TestMethod], so one check covers both.
        if (method.IsDefined(typeof(TestMethodAttribute), inherit: true))
            return FailureSite.TestBody;

        return null;
    }

    /// <summary>
    /// Determines how the current test ended, as seen from <c>[TestCleanup]</c>.
    /// </summary>
    /// <param name="context">The context of the test that just finished.</param>
    /// <returns>The outcome to record.</returns>
    /// <remarks>
    /// <para>
    /// A timeout is detected from the cancellation token, not from
    /// <see cref="TestContext.CurrentTestOutcome"/>, because MSTest never reports
    /// <see cref="UnitTestOutcome.Timeout"/> to cleanup. What it reports instead depends on how the
    /// test was written: <see cref="UnitTestOutcome.InProgress"/> when the test body was abandoned
    /// mid-run, <see cref="UnitTestOutcome.Failed"/> when it observed its cancellation token, and —
    /// for a cooperatively-cancelled test that ignored the token — <see cref="UnitTestOutcome.Passed"/>,
    /// even though the runner goes on to fail it. Reading the outcome alone therefore records a
    /// timed-out test as not-executed, failed, or passed depending on an unrelated detail.
    /// </para>
    /// <para>
    /// <c>IsCancellationRequested</c> is true for all three and false for every test that finished on
    /// its own, including tests that declare a generous timeout and stay well inside it. It is also
    /// true when the whole run is being torn down, which is still a test that was stopped rather than
    /// one that disagreed with an assertion, so the classification holds.
    /// </para>
    /// </remarks>
    private static TestOutcome ResolveOutcome(TestContext context)
    {
        if (context.CancellationTokenSource?.IsCancellationRequested == true)
            return TestOutcome.Timeout;

        return MapOutcome(context.CurrentTestOutcome);
    }

    private static TestOutcome MapOutcome(UnitTestOutcome outcome)
    {
        return outcome switch
        {
            UnitTestOutcome.Passed => TestOutcome.Passed,
            UnitTestOutcome.Failed => TestOutcome.Failed,
            UnitTestOutcome.Inconclusive => TestOutcome.Inconclusive,

            // Kept for completeness. MSTest does not surface this value to cleanup, so a real timeout
            // is classified by ResolveOutcome before reaching here.
            UnitTestOutcome.Timeout => TestOutcome.Timeout,
            UnitTestOutcome.Aborted => TestOutcome.Failed,
            _ => TestOutcome.NotExecuted
        };
    }

    /// <summary>
    /// Reads the timeout the test declared through <c>[Timeout]</c>.
    /// </summary>
    /// <param name="testMethod">The resolved test method, or <see langword="null"/> when it could not be found.</param>
    /// <returns>The declared budget and its source, or two nulls when the test declared none.</returns>
    private static (TimeSpan? Budget, TimeoutBudgetSource? Source) ResolveTimeoutBudget(MethodInfo? testMethod)
    {
        // inherit: true — [Timeout] on a base-class test method applies to the inherited test.
        var attribute = testMethod?.GetCustomAttribute<TimeoutAttribute>(inherit: true);
        if (attribute == null)
            return (null, null);

        int milliseconds = attribute.Timeout;

        // TestTimeout.Infinite is a declaration that the test may run without limit, which is not the
        // same as declaring nothing. Recorded as such so a reader can tell the two apart.
        if (milliseconds == (int)TestTimeout.Infinite || milliseconds <= 0)
            return (null, TimeoutBudgetSource.Infinite);

        return (TimeSpan.FromMilliseconds(milliseconds), TimeoutBudgetSource.Declared);
    }

    private static TestMetadata ExtractMetadata(TestContext context)
    {
        TestMetadataBuilder builder = new();

        builder.AddTag("framework:MSTest");

        // Extract properties
        if (context.Properties.Keys is { Count: > 0})
        {
            var hasDataRow = context.Properties.Contains("DataRow");

            foreach (var key in context.Properties.Keys)
            {
                var keyStr = key?.ToString();
                if (string.IsNullOrEmpty(keyStr))
                    continue;

                var value = context.Properties[keyStr]?.ToString() ?? string.Empty;

                if (keyStr == "TestCategory")
                    builder.AddCategory(value);
                else if (keyStr == "Description")
                    builder.WithDescription(value);
                else
                    builder.AddCustomAttribute(keyStr!, value);
            }

            if (hasDataRow)
            {
                builder.AddTag("type:datatest");
                var dataRowInfo = context.Properties["DataRow"]?.ToString();
                if (!string.IsNullOrEmpty(dataRowInfo))
                    builder.AddCustomAttribute("Arguments", dataRowInfo!);
            }
            else
            {
                builder.AddTag("type:test");
            }
        }
        else
        {
            builder.AddTag("type:test");
        }

        if (context.ResultsDirectory != null)
            builder.AddCustomAttribute("ResultsDirectory", context.ResultsDirectory);

        return builder.Build();
    }

    private static string ExtractAssemblyName(string fullyQualifiedClassName)
    {
        if (string.IsNullOrEmpty(fullyQualifiedClassName))
            return string.Empty;

        var firstDotIndex = fullyQualifiedClassName.IndexOf('.');
        return firstDotIndex >= 0 ? fullyQualifiedClassName.Substring(0, firstDotIndex) : fullyQualifiedClassName;
    }

    private static string? GetExceptionType(TestContext context) =>
        GetTestException(context)?.GetType().FullName;

    private static (string? stackTrace, bool stackTraceOmitted) ResolveStackTrace(
        TestOutcome outcome,
        string? stackTrace,
        bool captureStackTraces)
    {
        string? normalizedStackTrace = string.IsNullOrWhiteSpace(stackTrace) ? null : stackTrace;
        bool stackTraceAvailable = normalizedStackTrace != null;
        bool stackTraceOmitted = !captureStackTraces && outcome.IsFailure() && stackTraceAvailable;

        if (!captureStackTraces)
        {
            return (null, stackTraceOmitted);
        }

        return (normalizedStackTrace, false);
    }

    private static string? GetErrorMessage(TestContext context) =>
        GetTestException(context)?.Message;

    private static string? GetStackTrace(TestContext context) =>
        GetTestException(context)?.StackTrace;

    /// <summary>
    /// Returns the exception that failed the current test, or <see langword="null"/> when the test did
    /// not fail with one.
    /// </summary>
    /// <remarks>
    /// MSTest exposes the failure through <see cref="TestContext.TestException"/>, which it sets before
    /// <c>[TestCleanup]</c> runs. It is never populated from <c>TestContext.Properties</c>, which holds
    /// only the test name, the run directories, and any <c>[TestProperty]</c> or <c>DataRow</c> values.
    /// Outcomes with no exception behind them — a timeout, an aborted run — leave it null.
    /// </remarks>
    private static Exception? GetTestException(TestContext context)
    {
        if (context.CurrentTestOutcome == UnitTestOutcome.Passed)
            return null;

        Exception? exception = context.TestException;

        // Reflection-invoked test methods surface their failure wrapped; the wrapper's own message and
        // stack trace describe the invocation, not the test, so report what the test actually threw.
        return exception is TargetInvocationException { InnerException: not null } wrapper
            ? wrapper.InnerException
            : exception;
    }

    /// <summary>
    /// Extracts DataRow parameters from the MSTest TestContext.
    /// </summary>
    /// <remarks>
    /// MSTest exposes DataRow parameters through TestContext.TestData (introduced in MSTest 3.7).
    /// For non-parameterized tests, TestData is a null.
    /// </remarks>
    private static object[]? ExtractDataRowParameters(TestContext context)
    {
        try
        {
            // TestContext.TestData contains the parameters for DataRow-based tests (MSTest 3.7+)
            var testData = context.TestData;
            return testData is { Length: > 0 } ? (object[])testData : null;
        }
        catch
        {
            return null;
        }
    }

    private static string FormatTestNameWithParameters(string baseTestName, object[]? parameters)
    {
        if (parameters == null || parameters.Length == 0)
            return baseTestName;

        var formattedParams = new List<string>(parameters.Length);
        foreach (var param in parameters)
            formattedParams.Add(FormatParameterValue(param));

        return $"{baseTestName} ({string.Join(",", formattedParams)})";
    }

    private static string FormatParameterValue(object? parameter)
    {
        return parameter switch
        {
            null => "null",
            string str => str,
            bool b => b ? "true" : "false",
            byte b => b.ToString(CultureInfo.InvariantCulture),
            sbyte sb => sb.ToString(CultureInfo.InvariantCulture),
            short s => s.ToString(CultureInfo.InvariantCulture),
            ushort us => us.ToString(CultureInfo.InvariantCulture),
            int i => i.ToString(CultureInfo.InvariantCulture),
            uint ui => ui.ToString(CultureInfo.InvariantCulture),
            long l => l.ToString(CultureInfo.InvariantCulture),
            ulong ul => ul.ToString(CultureInfo.InvariantCulture),
            float f => f.ToString("G9", CultureInfo.InvariantCulture),
            double d => d.ToString("G17", CultureInfo.InvariantCulture),
            decimal dec => dec.ToString(CultureInfo.InvariantCulture),
            DateTime dt => dt.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToString("yyyy-MM-ddTHH:mm:ss.fffffffzzz", CultureInfo.InvariantCulture),
            Guid g => g.ToString("D"),
            _ => parameter.ToString() ?? "null"
        };
    }

    /// <summary>
    /// Locates the BCL <see cref="MethodInfo"/> for the currently running MSTest method by
    /// resolving the type via its fully qualified class name and stripping parameterized suffixes
    /// from the test name. Mirrors the pattern used in MSTestRetryDetector.
    /// </summary>
    private static MethodInfo? FindTestMethodForContext(TestContext context)
    {
        try
        {
            var fullClassName = context.FullyQualifiedTestClassName;
            if (string.IsNullOrEmpty(fullClassName))
            {
                return null;
            }

            Type? type = Type.GetType(fullClassName)
                ?? AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a =>
                    {
                        try { return a.GetTypes(); }
                        catch { return Array.Empty<Type>(); }
                    })
                    .FirstOrDefault(t => t.FullName == fullClassName);

            if (type == null)
            {
                return null;
            }

            var methodName = context.TestName ?? string.Empty;

            // Strip parameterized suffix: "MethodName (arg1, arg2)" → "MethodName"
            var parenIdx = methodName.IndexOf('(');
            if (parenIdx > 0)
            {
                methodName = methodName.Substring(0, parenIdx).Trim();
            }

            return type.GetMethods(
                    BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Instance | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == methodName);
        }
        catch
        {
            return null;
        }
    }
}
