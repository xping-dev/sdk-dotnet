/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Diagnostics;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
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
    private static readonly string[] _lineSeparators = ["\r\n", "\r", "\n"];

    // Resolved once in BeforeTest() and reused for every AfterTest() on this attribute instance.
    // The attribute can be applied at assembly, class, or method scope; in all cases the same
    // DI singletons are returned each time, so a single resolution per instance is enough.
    private XpingAttributeServices? _services;

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

            var execution = CreateTestExecution(_services!, test, startTime, endTime, duration, workerId, fixtureName);
            XpingContext.RecordTest(execution);
        }
        catch
        {
            // Swallow exceptions to avoid interfering with test execution
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

        TestIdentity identity = services.IdentityGenerator.Generate(
            fullyQualifiedName, assemblyName, parameters, displayName);

        var errorMessage = result.Message ?? string.Empty;
        var stackTrace = result.StackTrace ?? string.Empty;

        // Detect retry metadata first so the attempt number is available when claiming a position.
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
            .WithException(ExtractExceptionType(result), errorMessage, stackTrace)
            .WithErrorMessageHash(services.IdentityGenerator.GenerateErrorMessageHash(errorMessage))
            .WithStackTraceHash(services.IdentityGenerator.GenerateStackTraceHash(stackTrace))
            .WithTestOrchestrationRecord(orchestrationRecord)
            .WithRetry(retryMetadata)
            .Build();

        // Record test completion for tracking as previous test
        services.ExecutionTracker.RecordTestCompletion(workerId, identity.TestId, test.Name, outcome);

        return execution;
    }

    private static string? ExtractExceptionType(TestContext.ResultAdapter result)
    {
        // NUnit 3 doesn't expose the exception type directly through the public API.
        // We need to parse it from the message or stack trace
        if (result.Outcome.Status == TestStatus.Passed)
        {
            return null;
        }

        // Try to extract from the message - often contains an exception type
        var message = result.Message;
        if (!string.IsNullOrEmpty(message))
        {
            // Non-null after the check above
            var lines = message!.Split(_lineSeparators, StringSplitOptions.RemoveEmptyEntries);
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

        return TestOutcome.NotExecuted;
    }

    private static TestMetadata ExtractMetadata(ITest test)
    {
        TestMetadataBuilder builder = new();

        // Add common tags
        builder.AddTag("framework:nunit");
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
}
