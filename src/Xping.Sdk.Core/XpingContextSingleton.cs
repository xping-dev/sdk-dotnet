/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Exceptions;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Shared;

namespace Xping.Sdk.Core;

/// <summary>
/// The process-wide context behind an adapter's static <c>XpingContext</c> API: initialization, access,
/// and the claim that ends a session.
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

    // Serializes installing, claiming and forgetting, which each touch both _instance and _configuration:
    // without it a shutdown could forget the configuration between an install and the write that
    // remembers it. Reads stay lock-free, and so does Initialize() once a context is installed, which
    // the NUnit and MSTest adapters call before every test.
    private readonly object _lifecycleLock = new();
    private volatile Lazy<TContext>? _instance;

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
    {
        _create = create.RequireNotNull();
    }

    /// <summary>
    /// Gets a value indicating whether <see cref="Initialize"/> has been called and the context has not
    /// been shut down since. True before the host is built, which happens on first use.
    /// </summary>
    public bool IsInitialized => _instance != null;

    /// <summary>
    /// Gets the context, building its host on first access.
    /// </summary>
    /// <exception cref="InvalidOperationException">The context is not initialized.</exception>
    public TContext Instance =>
        (_instance ?? throw new InvalidOperationException(
            "XpingContext is not initialized. Call XpingContext.Initialize() first.")).Value;

    /// <summary>
    /// Gets the context if its host has been built, or <see langword="null"/>. Never builds the host.
    /// </summary>
    public TContext? Built => _instance is { IsValueCreated: true } instance ? instance.Value : null;

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
            _instance = new Lazy<TContext>(
                valueFactory: () => _create(resolved),
                LazyThreadSafetyMode.ExecutionAndPublication);

            // Remembered only when this call installed the context, so the configuration reused later is
            // one a session actually ran with.
            if (configuration != null)
                _configuration = configuration;
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

    // Uninstalls the context, so IsInitialized is false at once.
    private TContext? Claim()
    {
        Lazy<TContext>? instance;
        lock (_lifecycleLock)
        {
            instance = _instance;
            _instance = null;
        }

        return instance is { IsValueCreated: true } ? instance.Value : null;
    }
}
