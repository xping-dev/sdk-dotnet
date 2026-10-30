/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.IO.Compression;
using System.Text.Json;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.XUnit.V3.Probe;
using Xunit.Runner.Common;
using Xunit.Sdk;
using Xunit.v3;

namespace Xping.Sdk.XUnit.V3.Tests;

/// <summary>
/// Runs the probe assembly's tests through a real xUnit v3 pipeline with <see cref="XpingTestFramework"/>
/// as its framework, as a runner does, and reads back the session it stored.
/// </summary>
/// <remarks>
/// The session goes to a store of its own, in local-only mode: CI sets <c>XPING_APIKEY</c> for the
/// whole job, which would otherwise make this run upload its deliberately failing tests to Xping Cloud.
/// </remarks>
internal sealed class ProbeRun : IDisposable
{
    private readonly string _store = Path.Combine(Path.GetTempPath(), "xping-tests", Guid.NewGuid().ToString("N"));

    private ProbeRun()
    {
    }

    /// <summary>Gets every message the runner received, in order.</summary>
    public List<IMessageSinkMessage> Messages { get; } = [];

    /// <summary>Gets the session the run stored.</summary>
    public JsonElement Session { get; private set; }

    /// <summary>Gets how many sessions the run stored.</summary>
    public int SessionCount { get; private set; }

    /// <summary>Gets the stored executions, by test display name.</summary>
    public Dictionary<string, JsonElement> Executions { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Runs the probe's tests that pass <paramref name="filter"/>, after discovering all of them.
    /// </summary>
    /// <param name="filter">Which discovered test cases to run; all when <see langword="null"/>.</param>
    /// <param name="onMessage">Called for each message as the runner receives it.</param>
    /// <param name="runs">
    /// How many runs one framework serves, as it may in a long-lived process. The session read back is
    /// the only one when this is 1.
    /// </param>
    /// <param name="createFramework">Builds the framework under test; a default one when <see langword="null"/>.</param>
    public static async Task<ProbeRun> RunAsync(
        Func<ITestCase, bool>? filter = null,
        Action<IMessageSinkMessage>? onMessage = null,
        int runs = 1,
        Func<XpingTestFramework>? createFramework = null)
    {
        var run = new ProbeRun();

        await XpingContext.ShutdownAsync().ConfigureAwait(true);
        XpingContext.Initialize(new XpingConfiguration { Mode = XpingMode.LocalOnly, LocalStorePath = run._store });
        RetryTests.Reset();

        createFramework ??= () => new XpingTestFramework();
        XpingTestFramework framework = createFramework();
        try
        {
            for (int i = 0; i < runs; i++)
            {
                var controller = new InProcessFrontController(framework, typeof(OutcomeTests).Assembly, configFilePath: null);
                var configuration = new TestAssemblyConfiguration();
                using var cancellation = new CancellationTokenSource();

                await controller.FindAndRun(
                    new CollectingSink(run.Messages, onMessage),
                    TestFrameworkOptions.ForDiscovery(configuration),
                    TestFrameworkOptions.ForExecution(configuration),
                    filter ?? (_ => true),
                    cancellation).ConfigureAwait(true);
            }
        }
        finally
        {
            await framework.DisposeAsync().ConfigureAwait(true);
            await XpingContext.ShutdownAsync().ConfigureAwait(true);
        }

        run.SessionCount = SessionFiles(run._store).Length;
        if (runs > 1 || run.SessionCount == 0)
            return run;

        run.Session = ReadSession(run._store);
        foreach (JsonElement execution in run.Session.GetProperty("executions").EnumerateArray())
            run.Executions[execution.GetProperty("testName").GetString()!] = execution;

        return run;
    }

    /// <summary>Gets the stored execution of the probe test with this display name, without its namespace.</summary>
    public JsonElement Execution(string displayName) => Executions[$"{typeof(OutcomeTests).Namespace}.{displayName}"];

    /// <summary>Gets the probe assembly's statistics.</summary>
    public JsonElement AssemblyStatistics =>
        Session.GetProperty("statisticsByAssembly").GetProperty(typeof(OutcomeTests).Assembly.GetName().Name!);

    public void Dispose()
    {
        if (Directory.Exists(_store))
            Directory.Delete(_store, recursive: true);
    }

    private static JsonElement ReadSession(string store)
    {
        string session = Assert.Single(SessionFiles(store));

        using var gzip = new GZipStream(File.OpenRead(session), CompressionMode.Decompress);
        using JsonDocument document = JsonDocument.Parse(gzip);
        return document.RootElement.Clone();
    }

    private static string[] SessionFiles(string store)
    {
        string sessions = Path.Combine(store, "sessions");
        return Directory.Exists(sessions) ? Directory.GetFiles(sessions, "*.json.gz") : [];
    }

    private sealed class CollectingSink(List<IMessageSinkMessage> messages, Action<IMessageSinkMessage>? onMessage) : IMessageSink
    {
        public bool OnMessage(IMessageSinkMessage message)
        {
            lock (messages)
                messages.Add(message);

            onMessage?.Invoke(message);
            return true;
        }
    }
}
