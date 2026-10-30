/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Exceptions;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.Environment;
using Xping.Sdk.Core.Services.Environment.Internals;
using Xping.Sdk.Shared;

namespace Xping.Sdk.Core;

/// <summary>
/// The process-wide context behind an adapter's static <c>XpingContext</c> API: initialization, startup,
/// access, and the claim that ends a session.
/// </summary>
/// <typeparam name="TContext">The adapter's context type.</typeparam>
/// <remarks>
/// A helper the adapter holds, not a generic base class: the adapter has to supply how its context is
/// built, and netstandard2.0 has no static abstract members to ask for it. A static call made through a
/// derived type also never runs that type's static constructor, so a base class could not collect it there.
/// </remarks>
internal sealed class XpingContextSingleton<TContext>
    where TContext : XpingContextOrchestrator
{
    private readonly Func<XpingConfiguration?, TContext> _create;
    private readonly IEnvironmentVariableProvider _environment;

    // Serializes installing, claiming and forgetting, which each touch both _instance and _configuration:
    // without it a shutdown could forget the configuration between an install and the write that
    // remembers it. Reads stay lock-free, and so does Initialize() once a context is installed, which
    // the NUnit and MSTest adapters call before every test.
    private readonly object _lifecycleLock = new();
    private volatile Installation? _instance;

    // The configuration of the last context Initialize(configuration) installed, for the next session: the
    // session ends with each run, and a long-lived process, such as an IDE's test server, can start
    // another run through a parameterless Initialize(). Only ShutdownAsync forgets it.
    private XpingConfiguration? _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="XpingContextSingleton{TContext}"/> class.
    /// </summary>
    /// <param name="create">
    /// Builds the context from a configuration, or from appsettings.json and environment variables when
    /// it is <see langword="null"/>.
    /// </param>
    public XpingContextSingleton(Func<XpingConfiguration?, TContext> create)
        : this(create, new SystemEnvironmentVariableProvider())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="XpingContextSingleton{TContext}"/> class.
    /// </summary>
    /// <param name="create">See <see cref="XpingContextSingleton{TContext}(Func{XpingConfiguration?, TContext})"/>.</param>
    /// <param name="environment">Where <see cref="Start{T}"/> reads strict mode when the context cannot be built.</param>
    public XpingContextSingleton(Func<XpingConfiguration?, TContext> create, IEnvironmentVariableProvider environment)
    {
        _create = create.RequireNotNull();
        _environment = environment.RequireNotNull();
    }

    /// <summary>
    /// Gets a value indicating whether <see cref="Initialize"/> has been called and the context has not
    /// been shut down since. True before the host is built, which happens on first use.
    /// </summary>
    public bool IsInitialized => _instance != null;

    /// <summary>
    /// Gets the context, building its host on first access.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The context is not initialized, or it failed to start.
    /// </exception>
    public TContext Instance
    {
        get
        {
            Installation instance = _instance ?? throw new InvalidOperationException(
                "XpingContext is not initialized. Call XpingContext.Initialize() first.");

            if (instance.Failed)
                throw new InvalidOperationException("XpingContext failed to start; this session is not tracked.");

            return instance.Context.Value;
        }
    }

    /// <summary>
    /// Gets the context if its host has been built, or <see langword="null"/>. Never builds the host.
    /// </summary>
    public TContext? Built => _instance is { Failed: false, Context.IsValueCreated: true } instance
        ? instance.Context.Value
        : null;

    /// <summary>
    /// Installs a context, unless one is installed already. Its host is built on first use.
    /// </summary>
    /// <param name="configuration">
    /// The configuration to use, or <see langword="null"/> for the one the last
    /// <c>Initialize(configuration)</c> in this process installed, else appsettings.json and environment
    /// variables.
    /// </param>
    public void Initialize(XpingConfiguration? configuration)
    {
        if (IsInitialized)
            return;

        lock (_lifecycleLock)
        {
            if (_instance != null)
                return;

            XpingConfiguration? resolved = configuration ?? _configuration;
            _instance = new Installation(() => _create(resolved), resolved);

            // Remembered only when this call installed the context, so the configuration reused later is
            // one a session actually ran with.
            if (configuration != null)
                _configuration = configuration;
        }
    }

    /// <summary>
    /// Installs a context unless one is installed, builds it, and resolves what the adapter needs from it.
    /// The one place an adapter starts the SDK, so every adapter degrades the same way when it cannot.
    /// </summary>
    /// <typeparam name="T">What the adapter resolves.</typeparam>
    /// <param name="resolve">Resolves what the adapter needs from the built context.</param>
    /// <param name="report">
    /// Also told why the SDK could not start, for a runner channel the user is likelier to see than stderr.
    /// </param>
    /// <param name="retryNextRun">
    /// <see langword="true"/> when nothing ends a session that failed to start, as with xUnit, whose
    /// session ends in a message sink that only a started SDK installs: the failed context is uninstalled
    /// at once, so the next run in a long-lived process tries again. <see langword="false"/> when the
    /// session still ends, as with NUnit and MSTest, which call this before every test: the failure then
    /// lasts until the session ends instead of being retried, and reported, for every test.
    /// </param>
    /// <returns>
    /// What <paramref name="resolve"/> returned, or <see langword="null"/> when the SDK could not start and
    /// the run should go ahead untracked.
    /// </returns>
    /// <exception cref="XpingConfigurationException">
    /// Strict mode is on and the SDK could not start. The adapter must fail the run.
    /// </exception>
    public T? Start<T>(Func<TContext, T> resolve, Action<string>? report, bool retryNextRun)
        where T : class
    {
        resolve.RequireNotNull();

        Initialize(null);
        Installation? installation = _instance;

        // Shut down by another thread in between, or failed earlier this session.
        if (installation is null or { Failed: true })
            return null;

        try
        {
            return resolve(installation.Context.Value);
        }
        catch (Exception ex)
        {
            bool reportIt = Fail(installation, retryNextRun);

            // The context's constructor applies strict mode itself, from every source. A failure before
            // it ran, such as an appsettings.json that does not parse, left only the configuration passed
            // in code and the environment to ask.
            if (ex is XpingConfigurationException)
                throw;

            if (IsStrictMode(installation.Configuration))
                throw new XpingConfigurationException($"Failed to initialize Xping SDK: {ex.Message}", ex);

            if (reportIt)
            {
                string message = $"[Xping] SDK initialization failed: {ex.Message} Tests run without Xping tracking.";
                Console.Error.WriteLine(message);
                report?.Invoke(message);
            }

            return null;
        }
    }

    /// <summary>
    /// Records a test execution with the context.
    /// </summary>
    /// <param name="execution">The test execution to record.</param>
    /// <exception cref="InvalidOperationException">The context is not initialized.</exception>
    public void RecordTest(TestExecution execution) => Instance.RecordTestExecution(execution);

    /// <summary>
    /// Flushes the context's pending test executions.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">The context is not initialized.</exception>
    public Task FlushAsync() => Instance.FlushSessionAsync();

    /// <summary>
    /// Finalizes the session, if the context's host was built. Avoids building the host just to send an
    /// empty session when <see cref="Initialize"/> was called but no test ran.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task FinalizeAsync() => Built?.FinalizeSessionAsync(CancellationToken.None) ?? Task.CompletedTask;

    /// <summary>
    /// Finalizes the session and shuts the context down. See
    /// <see cref="XpingContextOrchestrator.EndSessionAsync(XpingContextOrchestrator?)"/>.
    /// </summary>
    /// <returns>
    /// The strict-mode network error the run must fail on, or <see langword="null"/> when there is none.
    /// </returns>
    public Task<XpingNetworkException?> EndSessionAsync()
    {
        // Claimed before finalizing, so the context finalized is the one shut down, even if Initialize()
        // installs a new one in between.
        return XpingContextOrchestrator.EndSessionAsync(Claim());
    }

    /// <summary>
    /// Disposes the context, if its host was built, uninstalls it, and forgets the remembered
    /// configuration, so the next <see cref="Initialize"/> starts from scratch.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async ValueTask ShutdownAsync()
    {
        TContext? context;
        lock (_lifecycleLock)
        {
            _configuration = null;
            context = Claim();
        }

        if (context != null)
            await context.DisposeAsync().ConfigureAwait(false);
    }

    // Uninstalls the context, so IsInitialized is false at once. A context that failed to start was
    // disposed when it failed, if it was built at all, so there is nothing to hand back.
    private TContext? Claim()
    {
        Installation? instance;
        lock (_lifecycleLock)
        {
            instance = _instance;
            _instance = null;
        }

        return instance is { Failed: false, Context.IsValueCreated: true } ? instance.Context.Value : null;
    }

    // Returns whether this call is the one that failed the installation, so concurrent callers that hit
    // the same cached failure report it once.
    private bool Fail(Installation installation, bool uninstall)
    {
        if (!installation.TryFail())
            return false;

        if (uninstall)
        {
            lock (_lifecycleLock)
            {
                if (_instance == installation)
                    _instance = null;
            }
        }

        // The host built and resolving from it failed. Nothing will finalize a context that failed to
        // start, so it is only released, off the caller's thread: this runs on the runner's startup path.
        if (installation.Context.IsValueCreated)
        {
            TContext context = installation.Context.Value;
            _ = Task.Run(async () =>
            {
                try { await context.DisposeAsync().ConfigureAwait(false); }
                catch { /* Releasing a context that never started must not surface anywhere. */ }
            });
        }

        return true;
    }

    private bool IsStrictMode(XpingConfiguration? configuration) =>
        configuration?.StrictMode == true ||
        IsTrue(_environment.GetVariable("XPING_STRICTMODE")) ||
        IsTrue(_environment.GetVariable("Xping__StrictMode"));

    private static bool IsTrue(string? value) => bool.TryParse(value, out bool parsed) && parsed;

    private sealed class Installation(Func<TContext> create, XpingConfiguration? configuration)
    {
        private int _failed;

        public Lazy<TContext> Context { get; } = new(create, LazyThreadSafetyMode.ExecutionAndPublication);

        // What the installation was built from, for strict mode when building it failed.
        public XpingConfiguration? Configuration { get; } = configuration;

        public bool Failed => Volatile.Read(ref _failed) != 0;

        public bool TryFail() => Interlocked.Exchange(ref _failed, 1) == 0;
    }
}
