/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Diagnostics;
using System.Reflection;
using Xping.Sdk.Core.Exceptions;
using Xping.Sdk.Core.Services.Statistics;
using Xunit.v3;

namespace Xping.Sdk.XUnit.V3;

/// <summary>
/// xUnit v3 test framework that records every test execution with Xping. Register it once per test
/// assembly with <c>[assembly: TestFramework(typeof(XpingTestFramework))]</c>.
/// </summary>
/// <remarks>
/// The session ends when the test assembly finishes, in the message sink the executor installs. xUnit
/// creates this framework in every process it starts, including the ones that only discover tests,
/// so the SDK host is built only once an executor is asked for.
/// </remarks>
public sealed class XpingTestFramework : XunitTestFramework
{
    private readonly string? _configFileName;

    // Shared by the discoverer and the executor this framework creates. The runner may ask for the
    // executor before it discovers, so the executor is handed the place a count will be, not a count.
    private readonly TestCaseCensus _census = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="XpingTestFramework"/> class.
    /// </summary>
    public XpingTestFramework()
        : this(configFileName: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="XpingTestFramework"/> class.
    /// </summary>
    /// <param name="configFileName">The optional xUnit configuration file, as xUnit passes it.</param>
    public XpingTestFramework(string? configFileName)
        : base(configFileName)
    {
        _configFileName = configFileName;

        try
        {
            XpingContext.Initialize();

            // Only here is the runner's own code on the call stack.
            XpingContext.RecordRunner(TestRunnerDetection.Detect(
                CallerTypeNames(),
                Environment.GetCommandLineArgs(),
                Environment.GetEnvironmentVariables().Keys.Cast<object>().Select(key => key.ToString())));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Xping] SDK initialization failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    protected override ITestFrameworkDiscoverer CreateDiscoverer(Assembly assembly) =>
        new XpingTestFrameworkDiscoverer(base.CreateDiscoverer(assembly), _census);

    /// <inheritdoc/>
    protected override ITestFrameworkExecutor CreateExecutor(Assembly assembly)
    {
        XpingExecutorServices services;
        try
        {
            // The session ends with each run, and a long-lived process, such as an IDE's test server,
            // may ask this framework for another. Initialize() does nothing while a session is open.
            XpingContext.Initialize();
            services = XpingContext.GetExecutorServices();
        }
        catch (XpingConfigurationException ex)
        {
            // Strict mode must fail the run visibly, and nothing has run yet, so no result is lost.
            // An exception would not do: the runner reports it and carries on without an executor's
            // worth of results, which a CI step can read as a pass.
            Environment.FailFast($"[Xping] Strict mode configuration error: {ex.Message}", ex);
            throw;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Xping] SDK initialization failed: {ex.Message}");
            return base.CreateExecutor(assembly);
        }

        return new XpingTestFrameworkExecutor(new XunitTestAssembly(assembly, _configFileName), services, _census);
    }

    private static IEnumerable<string?> CallerTypeNames() =>
        new StackTrace().GetFrames()?.Select(frame => frame?.GetMethod()?.DeclaringType?.FullName) ?? [];
}
