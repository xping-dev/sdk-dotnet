/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Moq;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.XUnit.Probe;
using Xping.Sdk.XUnit.ThrowingProbe;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Xping.Sdk.XUnit.Tests;

/// <summary>
/// Runs <see cref="XpingTestFrameworkProbe"/> through a real xUnit v2 pipeline with <see cref="XpingTestFramework"/>, as a
/// runner does: discover, then execute.
/// </summary>
[Collection("XpingContext")]
public sealed class XpingTestFrameworkTests : IAsyncLifetime
{
    private readonly string _scratchStore = Path.Combine(
        Path.GetTempPath(), "xping-tests", Guid.NewGuid().ToString("N"));

    public Task InitializeAsync() => XpingContext.ShutdownAsync().AsTask();

    public async Task DisposeAsync()
    {
        await XpingContext.ShutdownAsync().ConfigureAwait(false);
        AppContext.SetData(ModuleInitializerProbe.StoreKey, null);

        if (Directory.Exists(_scratchStore))
            Directory.Delete(_scratchStore, recursive: true);
    }

    [Fact]
    public void TheTestsStillRunUntrackedWhenTheSdkFailsToStart()
    {
        XpingContext.Initialize(ScratchConfiguration());

        List<IMessageSinkMessage> messages = Run(FailingToStart(new RecordingSink()));

        Assert.Single(messages.OfType<ITestPassed>());
        Assert.Empty(messages.OfType<IErrorMessage>());
        Assert.Equal(0, SessionCount());
    }

    [Fact]
    public void WhyTheSdkFailedToStartGoesToTheRunnersDiagnostics()
    {
        XpingContext.Initialize(ScratchConfiguration());
        var diagnostics = new RecordingSink();

        Run(FailingToStart(diagnostics));

        Assert.Contains(
            diagnostics.Messages.OfType<IDiagnosticMessage>(),
            m => m.Message.Contains("[Xping] SDK initialization failed: host failed to start", StringComparison.Ordinal));
    }

    /// <summary>
    /// A run that falls back never ends its session, so the failed context must not outlive it: in a
    /// long-lived process, such as an IDE's test server, every later run would stay untracked.
    /// </summary>
    [Fact]
    public void TheNextRunStartsTheSdkAgainAfterOneFailedTo()
    {
        XpingContext.Initialize(ScratchConfiguration());
        Run(FailingToStart(new RecordingSink()));

        List<IMessageSinkMessage> messages = Run(new XpingTestFramework(new RecordingSink()));

        Assert.Single(messages.OfType<ITestPassed>());
        Assert.Equal(1, SessionCount());
    }

    /// <summary>
    /// xUnit v2 creates the framework, and the framework starts the session, before any code in the test
    /// assembly runs. A configuration passed from the assembly's <c>[ModuleInitializer]</c> must still be
    /// the one the session uses.
    /// </summary>
    [Fact]
    public void TheSessionUsesTheConfigurationFromTheTestAssemblysModuleInitializer()
    {
        AppContext.SetData(ModuleInitializerProbe.StoreKey, _scratchStore);

        // By name: naming a probe type in this method would run the probe's initializer when the method
        // is compiled, before the store is set.
        Assembly probe = Assembly.Load("Xping.Sdk.XUnit.Probe");

        List<IMessageSinkMessage> messages = Run(
            new XpingTestFramework(new RecordingSink()),
            probe,
            "Xping.Sdk.XUnit.Probe.ModuleInitializerProbeTests");

        Assert.Single(messages.OfType<ITestPassed>());
        Assert.Equal(1, SessionCount());
    }

    /// <summary>
    /// A module initializer that throws is the test assembly's error, not the SDK's: the SDK says so in
    /// the runner's diagnostics and still starts, and xUnit reports the failure when it runs the tests.
    /// </summary>
    [Fact]
    public void AThrowingModuleInitializerIsReportedAndTheRunStillCompletes()
    {
        XpingContext.Initialize(ScratchConfiguration());
        var diagnostics = new RecordingSink();

        // By name, for the same reason as the probe above.
        Assembly probe = Assembly.Load("Xping.Sdk.XUnit.ThrowingProbe");

        List<IMessageSinkMessage> messages = Run(
            new XpingTestFramework(diagnostics),
            probe,
            "Xping.Sdk.XUnit.ThrowingProbe.ThrowingModuleInitializerProbeTests");

        Assert.Contains(
            diagnostics.Messages.OfType<IDiagnosticMessage>(),
            m => m.Message.StartsWith(
                "[Xping] The module initializer of Xping.Sdk.XUnit.ThrowingProbe failed: ",
                StringComparison.Ordinal));
        ITestFailed failed = Assert.Single(messages.OfType<ITestFailed>());
        Assert.Contains(ThrowingModuleInitializerProbe.Message, string.Join(" ", failed.Messages), StringComparison.Ordinal);
        Assert.Equal(1, SessionCount());
    }

    private static XpingTestFramework FailingToStart(IMessageSink diagnostics) =>
        new(diagnostics, _ => throw new InvalidOperationException("host failed to start"));

    private static List<IMessageSinkMessage> Run(XpingTestFramework framework) =>
        Run(framework, typeof(XpingTestFrameworkProbe).Assembly, typeof(XpingTestFrameworkProbe).FullName!);

    private static List<IMessageSinkMessage> Run(XpingTestFramework framework, Assembly assembly, string testClass)
    {
        using (framework)
        {
            var discovery = new RecordingSink();
            using (ITestFrameworkDiscoverer discoverer = framework.GetDiscoverer(Reflector.Wrap(assembly)))
            {
                discoverer.Find(testClass, false, discovery, Mock.Of<ITestFrameworkDiscoveryOptions>());
                discovery.WaitFor<IDiscoveryCompleteMessage>();
            }

            var run = new RecordingSink();
            using ITestFrameworkExecutor executor = framework.GetExecutor(assembly.GetName());
            executor.RunTests(
                discovery.Messages.OfType<ITestCaseDiscoveryMessage>().Select(m => m.TestCase).ToList(),
                run,
                Mock.Of<ITestFrameworkExecutionOptions>());
            run.WaitFor<ITestAssemblyFinished>();

            return run.Messages;
        }
    }

    private int SessionCount()
    {
        string sessions = Path.Combine(_scratchStore, "sessions");
        return Directory.Exists(sessions) ? Directory.GetFiles(sessions, "*.json.gz").Length : 0;
    }

    private XpingConfiguration ScratchConfiguration() =>
        new() { Mode = XpingMode.LocalOnly, LocalStorePath = _scratchStore };

    private sealed class RecordingSink : LongLivedMarshalByRefObject, IMessageSink
    {
        private readonly List<IMessageSinkMessage> _messages = [];

        public List<IMessageSinkMessage> Messages
        {
            get
            {
                lock (_messages)
                    return [.. _messages];
            }
        }

        public bool OnMessage(IMessageSinkMessage message)
        {
            lock (_messages)
            {
                _messages.Add(message);
                Monitor.PulseAll(_messages);
            }

            return true;
        }

        public void WaitFor<T>()
        {
            lock (_messages)
            {
                while (!_messages.OfType<T>().Any())
                {
                    if (!Monitor.Wait(_messages, TimeSpan.FromSeconds(30)))
                        throw new TimeoutException($"No {typeof(T).Name} within 30 seconds.");
                }
            }
        }
    }
}

/// <summary>
/// What <see cref="XpingTestFrameworkTests"/> runs. It also runs with the rest of this project's tests,
/// where it just passes.
/// </summary>
[SuppressMessage("Design", "CA1515", Justification = "xUnit v2 discovers only public test classes")]
public sealed class XpingTestFrameworkProbe
{
    [Fact]
    public void Passes()
    {
    }
}
