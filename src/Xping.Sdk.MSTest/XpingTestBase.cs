/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Diagnostics;
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Executions;

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
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Xping] SDK initialization failed: {ex.Message}");
        }

        _startTime = DateTime.UtcNow;
        _startTimestamp = Stopwatch.GetTimestamp();
    }

    /// <summary>
    /// Cleans up after each test and records the test execution.
    /// </summary>
    [TestCleanup]
    public void XpingTestCleanup()
    {
        if (TestContext == null)
            return;

        var endTimestamp = Stopwatch.GetTimestamp();
        var endTime = DateTime.UtcNow;

        var elapsedTicks = endTimestamp - _startTimestamp;
        var duration = TimeSpan.FromTicks(elapsedTicks * TimeSpan.TicksPerSecond / Stopwatch.Frequency);

        var threadId = Environment.CurrentManagedThreadId.ToString(CultureInfo.InvariantCulture);
        var className = TestContext.FullyQualifiedTestClassName ?? "Unknown";

        try
        {
            var execution = CreateTestExecution(_services, TestContext, _startTime, endTime, duration, threadId, className);
            XpingContext.RecordTest(execution);
        }
        catch
        {
            // Swallow exceptions to avoid interfering with test execution
        }
    }

    private static TestExecution CreateTestExecution(
        XpingBaseServices services,
        TestContext context,
        DateTime startTime,
        DateTime endTime,
        TimeSpan duration,
        string threadId,
        string className)
    {
        var outcome = MapOutcome(context.CurrentTestOutcome);

        // Extract assembly from a fully qualified class name
        var fullClassName = context.FullyQualifiedTestClassName ?? string.Empty;
        var assemblyName = ExtractAssemblyName(fullClassName);

        // Build the fully qualified test name
        var fullyQualifiedName = $"{context.FullyQualifiedTestClassName}.{context.TestName}";

        // Extract DataRow parameters if present (MSTest 3.7+)
        object[]? parameters = ExtractDataRowParameters(context);

        // Format test name with parameters to match NUnit and xUnit display format
        var testName = FormatTestNameWithParameters(context.TestName ?? "Unknown", parameters);

        // Generate stable test identity
        TestIdentity identity = services.IdentityGenerator.Generate(
            fullyQualifiedName, assemblyName, parameters, testName);

        var errorMessage = GetErrorMessage(context) ?? string.Empty;
        var stackTrace = GetStackTrace(context) ?? string.Empty;

        // Detect retry metadata first so the attempt number is available when claiming a position.
        RetryMetadata? retryMetadata = services.RetryDetector.DetectRetryMetadata(context, outcome);

        // Create an execution context using ExecutionTracker.
        // Pass the attempt number so retried executions reuse the position of the first attempt.
        var orchestrationRecord = services.ExecutionTracker.CreateExecutionContext(
            threadId, className, retryMetadata?.AttemptNumber ?? 1);

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
            .WithException(GetExceptionType(context), errorMessage, stackTrace)
            .WithErrorMessageHash(services.IdentityGenerator.GenerateErrorMessageHash(errorMessage))
            .WithStackTraceHash(services.IdentityGenerator.GenerateStackTraceHash(stackTrace))
            .WithTestOrchestrationRecord(orchestrationRecord)
            .WithRetry(retryMetadata)
            .Build();

        // Record test completion for tracking as previous test
        services.ExecutionTracker.RecordTestCompletion(threadId, identity.TestFingerprint, testName, outcome);

        return execution;
    }

    private static TestOutcome MapOutcome(UnitTestOutcome outcome)
    {
        return outcome switch
        {
            UnitTestOutcome.Passed => TestOutcome.Passed,
            UnitTestOutcome.Failed => TestOutcome.Failed,
            UnitTestOutcome.Inconclusive => TestOutcome.Inconclusive,
            UnitTestOutcome.Timeout => TestOutcome.Failed,
            UnitTestOutcome.Aborted => TestOutcome.Failed,
            _ => TestOutcome.NotExecuted
        };
    }

    private static TestMetadata ExtractMetadata(TestContext context)
    {
        TestMetadataBuilder builder = new();

        builder.AddTag("framework:mstest");

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

    private static string? GetExceptionType(TestContext context)
    {
        if (context.CurrentTestOutcome == UnitTestOutcome.Passed)
            return null;

        if (context.Properties.Keys is { Count: >0 } && context.Properties.Contains("ExceptionType"))
            return context.Properties["ExceptionType"]?.ToString();

        return null;
    }

    private static string? GetErrorMessage(TestContext context)
    {
        if (context.CurrentTestOutcome == UnitTestOutcome.Passed)
            return null;

        if (context.Properties.Keys is { Count: >0 } && context.Properties.Contains("ExceptionMessage"))
            return context.Properties["ExceptionMessage"]?.ToString();

        return null;
    }

    private static string? GetStackTrace(TestContext context)
    {
        if (context.CurrentTestOutcome == UnitTestOutcome.Passed)
            return null;

        if (context.Properties.Keys is { Count: >0 } && context.Properties.Contains("StackTrace"))
            return context.Properties["StackTrace"]?.ToString();

        return null;
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
}
