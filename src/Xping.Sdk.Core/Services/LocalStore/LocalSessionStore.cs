/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xping.Sdk.Core.Services.LocalStore.Internals;

namespace Xping.Sdk.Core.Services.LocalStore;

/// <summary>
/// Creates <see cref="ILocalSessionStore"/> instances over the on-disk local session store.
/// </summary>
/// <remarks>
/// This is the supported entry point for reading full sessions from outside the SDK — the Xping CLI
/// uses it as the substrate for local analysis. The concrete implementation stays internal so its
/// file layout and retention behaviour can change without breaking callers.
/// </remarks>
public static class LocalSessionStore
{
    /// <summary>
    /// Creates a store over the default location for the current repository.
    /// </summary>
    /// <param name="options">Retention settings. Defaults are used when <see langword="null"/>.</param>
    /// <param name="logger">Diagnostics sink. Storage problems are logged here at debug level.</param>
    /// <param name="startDirectory">
    /// Directory to begin repository-root discovery from. Defaults to the entry assembly's location.
    /// </param>
    /// <returns>A store, which may report <see cref="ILocalSessionStore.IsAvailable"/> as false.</returns>
    public static ILocalSessionStore Create(
        LocalStoreOptions? options = null,
        ILogger? logger = null,
        string? startDirectory = null) =>
        new JsonSessionStore(
            options ?? new LocalStoreOptions(),
            logger ?? NullLogger.Instance,
            startDirectory);
}
