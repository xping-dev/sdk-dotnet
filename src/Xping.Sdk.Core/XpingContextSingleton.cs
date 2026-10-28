/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Exceptions;
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
    private Lazy<TContext>? _instance;

    // The configuration the last session was started with, for the next one: the session ends with each
    // run, and a long-lived process, such as an IDE's test server, can start another run.
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
    /// <exception cref="ArgumentNullException">The context is not initialized.</exception>
    public TContext Instance => _instance.RequireNotNull().Value;

    /// <summary>
    /// Gets the context if its host has been built, or <see langword="null"/>. Never builds the host.
    /// </summary>
    public TContext? Built => _instance is { IsValueCreated: true } instance ? instance.Value : null;

    /// <summary>
    /// Installs a context, unless one is installed already. Its host is built on first use.
    /// </summary>
    /// <param name="configuration">
    /// The configuration to use, or <see langword="null"/> for the one an earlier session in this process
    /// was initialized with, else appsettings.json and environment variables.
    /// </param>
    public void Initialize(XpingConfiguration? configuration)
    {
        if (IsInitialized)
            return;

        if (configuration != null)
            Volatile.Write(ref _configuration, configuration);
        else
            configuration = Volatile.Read(ref _configuration);

        Lazy<TContext> newInstance = new(
            valueFactory: () => _create(configuration),
            LazyThreadSafetyMode.ExecutionAndPublication);

        Interlocked.CompareExchange(ref _instance, newInstance, null);
    }

    /// <summary>
    /// Gets the context for resolving an adapter's services, building its host on first access.
    /// </summary>
    /// <param name="services">What the caller resolves, for the error message.</param>
    /// <returns>The context.</returns>
    /// <exception cref="InvalidOperationException">The context is not initialized.</exception>
    public TContext Require(string services)
    {
        Lazy<TContext>? instance = _instance;
        if (instance == null)
            throw new InvalidOperationException(
                $"XpingContext must be initialized before resolving {services}. " +
                "Ensure Initialize() has been called.");

        return instance.Value;
    }

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
    /// Disposes the context, if its host was built, and uninstalls it so <see cref="Initialize"/> can
    /// install a new one.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async ValueTask ShutdownAsync()
    {
        TContext? context = Claim();
        if (context != null)
            await context.DisposeAsync().ConfigureAwait(false);
    }

    // Uninstalls the context atomically, so IsInitialized is false at once.
    private TContext? Claim()
    {
        Lazy<TContext>? instance = Interlocked.Exchange(ref _instance, null);
        return instance is { IsValueCreated: true } ? instance.Value : null;
    }
}
