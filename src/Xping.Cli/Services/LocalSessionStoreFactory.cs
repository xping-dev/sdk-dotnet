/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.Logging;
using Xping.Sdk.Core.Services.LocalStore;

namespace Xping.Cli.Services;

/// <summary>
/// Creates <see cref="ILocalSessionStore"/> instances for a CLI invocation.
/// </summary>
/// <remarks>
/// A store's options and start directory come from parsed, per-command arguments (<c>--runs</c>,
/// <c>--directory</c>), so it cannot be registered as a single shared DI instance. This factory is
/// the shared singleton instead, giving every command the same <see cref="ILoggerFactory"/> wiring
/// without duplicating the underlying <see cref="LocalSessionStore.Create"/> call site.
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
