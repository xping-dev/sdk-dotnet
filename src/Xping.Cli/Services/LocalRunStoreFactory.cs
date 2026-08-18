/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.Logging;
using Xping.Sdk.Core.Services.LocalStore;

namespace Xping.Cli.Services;

/// <summary>
/// Creates <see cref="ILocalRunStore"/> instances for a CLI invocation.
/// </summary>
/// <remarks>
/// A store's options and start directory come from parsed, per-command arguments (<c>--last</c>,
/// <c>--directory</c>), so it cannot be registered as a single shared DI instance. This factory is
/// the shared singleton instead, giving every command the same <see cref="ILoggerFactory"/> wiring
/// without duplicating the underlying <see cref="LocalRunStore.Create"/> call site.
/// </remarks>
internal interface ILocalRunStoreFactory
{
    ILocalRunStore Create(LocalStoreOptions? options = null, string? startDirectory = null);
}

internal sealed class LocalRunStoreFactory(ILoggerFactory loggerFactory) : ILocalRunStoreFactory
{
    public ILocalRunStore Create(LocalStoreOptions? options = null, string? startDirectory = null) =>
        LocalRunStore.Create(options, loggerFactory.CreateLogger("Xping.Cli.LocalRunStore"), startDirectory);
}

/// <summary>
/// Creates <see cref="ILocalSessionStore"/> instances for a CLI invocation.
/// </summary>
/// <remarks>
/// Separate from <see cref="ILocalRunStoreFactory"/> because the two tiers answer different
/// questions. The run tier holds a slim projection sized for a fast summary; the session tier holds
/// whole sessions and is what local analysis reads. A single factory returning both would invite
/// callers to reach for whichever came to hand.
/// </remarks>
internal interface ILocalSessionStoreFactory
{
    /// <summary>
    /// Creates a store.
    /// </summary>
    /// <param name="options">Retention settings, or <see langword="null"/> for the defaults.</param>
    /// <param name="startDirectory">Directory to resolve the store from.</param>
    /// <returns>The store, which may report itself unavailable.</returns>
    ILocalSessionStore Create(LocalStoreOptions? options = null, string? startDirectory = null);
}

internal sealed class LocalSessionStoreFactory(ILoggerFactory loggerFactory) : ILocalSessionStoreFactory
{
    public ILocalSessionStore Create(LocalStoreOptions? options = null, string? startDirectory = null) =>
        LocalSessionStore.Create(
            options, loggerFactory.CreateLogger("Xping.Cli.LocalSessionStore"), startDirectory);
}
