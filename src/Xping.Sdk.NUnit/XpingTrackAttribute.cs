/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Diagnostics;
using System.Reflection;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using Xping.Sdk.Core.Attributes;
using Xping.Sdk.Core.Exceptions;
using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Executions;

namespace Xping.Sdk.NUnit;

/// <summary>
/// NUnit attribute for tracking test execution with Xping.
/// Can be applied to test methods or test fixtures.
/// </summary>
/// <remarks>
/// <para>
/// This attribute only tracks tests that actually execute. Tests marked with
/// the [Ignore] attribute are skipped by NUnit before execution begins and
/// will not be tracked by Xping.
/// </para>
/// </remarks>
[AttributeUsage(
    AttributeTargets.Method
    | AttributeTargets.Class
    | AttributeTargets.Assembly)]
public sealed class XpingTrackAttribute : Attribute, ITestAction
{
    private const string StartTimeKey = "Xping.StartTime";
    private const string StartTimestampKey = "Xping.StartTimestamp";

    // Header NUnit writes for an Assert.Multiple failure. Identical in NUnit 3.14 and 4.x.
    private const string MultipleFailureHeader = "Multiple failures or warnings in test:";

    // NUnit prefixes a setup or teardown failure with "SetUp : " / "TearDown : ", so at most the
    // first two segments of the line can hold the type name.
    private const int MaxTypeSegmentsScanned = 2;

    private static readonly string[] _lineSeparators = ["\r\n", "\r", "\n"];

    // Separator ExceptionHelper.BuildMessage places between the type name and the message.
    private static readonly string[] _typeMessageSeparators = [" : "];

    private static readonly string _assertionExceptionTypeName = typeof(AssertionException).FullName!;

    private static readonly string _multipleAssertExceptionTypeName =
        typeof(MultipleAssertException).FullName!;

    // Resolved once in BeforeTest() and reused for every AfterTest() on this attribute instance.
    // The attribute can be applied at assembly, class, or method scope; in all cases the same
    // DI singletons are returned each time, so a single resolution per instance is enough.
    private XpingAttributeServices _services = null!;

    /// <summary>
    /// Gets the action targets (Test level).
    /// </summary>
    ActionTargets ITestAction.Targets => ActionTargets.Test;

    /// <summary>
    /// Called before each test execution.
    /// </summary>
    /// <param name="test">The test being executed.</param>
    void ITestAction.BeforeTest(ITest test)
    {
        if (!XpingContext.IsInitialized)
        {
            XpingContext.Initialize();
        }

        // Resolve and cache services once per attribute instance. GetAttributeServices()
        // materializes the Lazy<XpingContext> on the first call (building the DI host), so
        // later calls on the same instance are a no-op field read.
        try
        {
            _services ??= XpingContext.GetAttributeServices();
        }
        catch (XpingConfigurationException ex)
        {
            // Re-throwing from BeforeTest only fails the current test; NUnit still attempts
            // every subsequent test. FailFast aborts the process immediately, which is the
            // correct behavior for strict mode where observability must be guaranteed.
            Environment.FailFast($"[Xping] Strict mode configuration error: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Xping] SDK initialization failed: {ex.Message}");
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

        try
        {
            // Mark the test in flight so the tests it overlaps with can be measured. Reported under
            // the same worker key AfterTest uses, so the start and end pair up. No per-test state is
            // needed here: the tracker keys in-flight tests by worker, so applying [XpingTrack] at
            // several scopes at once (which invokes BeforeTest once per attribute instance) cannot
            // inflate the count.
            _services.ExecutionTracker.RecordTestStart(TestContext.CurrentContext.WorkerId);
        }
        catch
        {
            // Swallow exceptions to avoid interfering with test execution: _services is unset when
            // the SDK failed to initialize above.
        }
    }

    /// <summary>
    /// Called after each test execution.
    /// </summary>
    /// <param name="test">The test that was executed.</param>
    void ITestAction.AfterTest(ITest? test)
    {
        if (test == null)
        {
            return;
        }

        var endTime = DateTime.UtcNow;
        var endTimestamp = Stopwatch.GetTimestamp();

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
            var duration = TimeSpan.FromTicks(elapsedTicks * TimeSpan.TicksPerSecond / Stopwatch.Frequency);

            // Extract WorkerId from NUnit context (available for parallel execution)
            var workerId = TestContext.CurrentContext.WorkerId;
            var fixtureName = test.TypeInfo?.FullName ?? test.ClassName;

            var execution = CreateTestExecution(_services, test, startTime, endTime, duration, workerId, fixtureName);
            XpingContext.RecordTest(execution);
        }
        catch
        {
            // Swallow exceptions to avoid interfering with test execution
        }
        finally
        {
            try
            {
                // Release the in-flight slot even when recording failed, so later tests are not
                // reported as having run concurrently with this one. Runs after the record above was
                // built, so this test still counts itself.
                _services.ExecutionTracker.RecordTestEnd(TestContext.CurrentContext.WorkerId);
            }
            catch
            {
                // Swallow exceptions to avoid interfering with test execution
            }
        }
    }

    private static TestExecution CreateTestExecution(
        XpingAttributeServices services,
        ITest test,
        DateTime startTime,
        DateTime endTime,
        TimeSpan duration,
        string? workerId,
        string? fixtureName)
    {
        var result = TestContext.CurrentContext.Result;
        var outcome = MapOutcome(result.Outcome);

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

        // Read the pinned fingerprint from [XpingFingerprint] if present on the test method
        string? pinnedFingerprint = ReadPinnedFingerprint(test.Method?.MethodInfo);

        TestIdentity identity = services.IdentityGenerator.Generate(
            fullyQualifiedName,
            assemblyName,
            parameters,
            displayName,
            testFingerprint: pinnedFingerprint);

        var errorMessage = result.Message ?? string.Empty;
        string? stackTrace = string.IsNullOrWhiteSpace(result.StackTrace) ? null : result.StackTrace;
        (string? configuredStackTrace, bool stackTraceOmitted) =
            ResolveStackTrace(outcome, stackTrace, services.CaptureStackTraces);

        // Detect retry metadata first, so the attempt number is available when claiming a position.
        RetryMetadata? retryMetadata = services.RetryDetector.DetectRetryMetadata(test, outcome);

        // Create an execution context using ExecutionTracker.
        // Pass the attempt number so retried executions reuse the position of the first attempt.
        var orchestrationRecord = services.ExecutionTracker.CreateExecutionContext(
            workerId, fixtureName, retryMetadata?.AttemptNumber ?? 1);

        TestMetadata metadata = ExtractMetadata(test);

        TestExecution execution = new TestExecutionBuilder()
            .WithExecutionId(Guid.NewGuid())
            .WithIdentity(identity)
            .WithTestName(test.Name)
            .WithOutcome(outcome)
            .WithDuration(duration)
            .WithStartTime(startTime)
            .WithEndTime(endTime)
            .WithMetadata(metadata)
            .WithException(ExtractExceptionType(result.Outcome, result.Message), errorMessage, configuredStackTrace)
            .WithErrorMessageHash(services.IdentityGenerator.GenerateErrorMessageHash(errorMessage))
            .WithStackTraceHash(services.IdentityGenerator.GenerateStackTraceHash(configuredStackTrace))
            .WithStackTraceOmitted(stackTraceOmitted)
            .WithTestOrchestrationRecord(orchestrationRecord)
            .WithRetry(retryMetadata)
            .Build();

        // Record test completion for tracking as previous test
        services.ExecutionTracker.RecordTestCompletion(workerId, identity.TestFingerprint, test.Name, outcome);

        return execution;
    }

    private static (string? stackTrace, bool stackTraceOmitted) ResolveStackTrace(
        TestOutcome outcome,
        string? stackTrace,
        bool captureStackTraces)
    {
        bool stackTraceAvailable = !string.IsNullOrEmpty(stackTrace);
        bool stackTraceOmitted = !captureStackTraces && outcome == TestOutcome.Failed && stackTraceAvailable;

        if (!captureStackTraces)
        {
            return (null, stackTraceOmitted);
        }

        return (stackTrace, false);
    }

    /// <summary>
    /// Determines the exception type for a completed test from its <see cref="ResultState"/>.
    /// </summary>
    /// <param name="outcome">The NUnit result state of the test, if one was recorded.</param>
    /// <param name="message">The NUnit failure message, if any.</param>
    /// <returns>The full exception type name, or <c>null</c> when NUnit records no type.</returns>
    /// <remarks>
    /// <para>
    /// <see cref="ITestAction.AfterTest(ITest)"/> only receives <see cref="TestContext.ResultAdapter"/>,
    /// which exposes no exception object, so the type is derived from the outcome rather than guessed
    /// from the message. Only the error arm carries a type name in its text.
    /// </para>
    /// </remarks>
    internal static string? ExtractExceptionType(ResultState? outcome, string? message)
    {
        if (outcome is null || outcome.Status != TestStatus.Failed)
        {
            // Passed, Skipped, Inconclusive and Warning results carry no exception type.
            return null;
        }

        // Assertion failures: NUnit throws AssertionException and writes only the assertion text
        // into the message, so there is nothing to parse and nothing worth guessing.
        // Matches() compares Status and Label while ignoring Site, so SetUpFailure lands here too.
        if (outcome.Matches(ResultState.Failure))
        {
            if (outcome.Site == FailureSite.Child)
            {
                // A suite rollup: the failing type belongs to a child test, not to this one.
                return null;
            }

            // Assert.Multiple throws MultipleAssertException, which is a sibling of
            // AssertionException rather than a subclass. The header is the only way to tell them apart.
            return StartsWithMultipleFailureHeader(message)
                ? _multipleAssertExceptionTypeName
                : _assertionExceptionTypeName;
        }

        // Unhandled exceptions: NUnit builds these messages with ExceptionHelper.BuildMessage,
        // which writes "{FullTypeName} : {message}". This is the only arm that encodes a real type.
        if (outcome.Matches(ResultState.Error))
        {
            return ParseExceptionTypeFromErrorMessage(message);
        }

        // Cancelled, NotRunnable and any future label: no type is known.
        return null;
    }

    private static bool StartsWithMultipleFailureHeader(string? message) =>
        message != null &&
        message.TrimStart().StartsWith(MultipleFailureHeader, StringComparison.Ordinal);

    /// <summary>
    /// Reads the exception type out of a message NUnit formatted as "{FullTypeName} : {message}".
    /// </summary>
    private static string? ParseExceptionTypeFromErrorMessage(string? message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return null;
        }

        // Non-null after the check above.
        var lines = message!.Split(_lineSeparators, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
        {
            return null;
        }

        var segments = lines[0].Trim().Split(_typeMessageSeparators, StringSplitOptions.None);

        // The last segment is the message body: a type token has to be followed by " : ".
        // Scanning the leading segments only lets a "SetUp : {type} : {message}" prefix through
        // without hunting for type-shaped words deep inside the prose.
        var limit = Math.Min(segments.Length - 1, MaxTypeSegmentsScanned);
        for (var i = 0; i < limit; i++)
        {
            var candidate = segments[i].Trim();
            if (IsExceptionTypeName(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool IsExceptionTypeName(string candidate) =>
        candidate.Length > 0 &&
        candidate.IndexOf('.') > 0 &&
        candidate.IndexOf(' ') < 0;

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

        return TestOutcome.NotExecuted;
    }

    private static TestMetadata ExtractMetadata(ITest test)
    {
        TestMetadataBuilder builder = new();

        // Add common tags
        builder.AddTag("framework:NUnit");
        builder.AddTag(test.IsSuite ? "type:suite" : "type:test");

        // Add a fixture type name if available
        if (test.TypeInfo != null)
        {
            builder.AddCustomAttribute("FixtureType", test.TypeInfo.FullName);
        }

        // Extract categories
        if (test.Properties.ContainsKey("Category"))
        {
            var categoryValues = test.Properties["Category"];
            foreach (var category in categoryValues)
            {
                if (category != null)
                {
                    builder.AddCategory(category.ToString()!);
                }
            }
        }

        // Extract description
        if (test.Properties.ContainsKey("Description"))
        {
            var descriptions = test.Properties["Description"];
            if (descriptions.Count > 0 && descriptions[0] != null)
            {
                builder.WithDescription(descriptions[0].ToString());
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
                    builder.AddTag($"author:{author}");
                }
            }
        }

        // Extract test case arguments if parameterized
        if (test.Properties.ContainsKey("Arguments"))
        {
            var args = test.Properties["Arguments"];
            if (args.Count > 0 && args[0] != null)
            {
                builder.AddCustomAttribute("Arguments", args[0].ToString()!);
            }
        }

        TestMetadata metadata = builder.Build();
        return metadata;
    }

    /// <summary>
    /// Reads the pinned fingerprint from <see cref="XpingFingerprintAttribute"/> on the test method.
    /// Returns null when the attribute is absent (SHA256 will be computed instead).
    /// </summary>
    private static string? ReadPinnedFingerprint(MethodInfo? methodInfo)
    {
        if (methodInfo == null)
        {
            return null;
        }

        return methodInfo.GetCustomAttribute<XpingFingerprintAttribute>(inherit: false)?.Fingerprint;
    }
}
