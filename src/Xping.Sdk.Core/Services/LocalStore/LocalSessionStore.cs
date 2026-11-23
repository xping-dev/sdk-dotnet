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
/// The Xping CLI reads full sessions through this for local analysis; the SDK itself only writes
/// them, through DI. Going through the interface keeps the CLI off the file layout, which
/// <see cref="JsonSessionStore"/> owns.
/// </remarks>
internal static class LocalSessionStore
{
    /// <summary>
    /// Creates a store over the given location, or the default one for the current repository.
    /// </summary>
    /// <param name="options">Retention settings. Defaults are used when <see langword="null"/>.</param>
    /// <param name="logger">Diagnostics sink. Storage problems are logged here at debug level.</param>
    /// <param name="startDirectory">
    /// Directory to begin repository-root discovery from. Defaults to the SDK assembly's folder.
    /// </param>
    /// <param name="storePath">
    /// The store root, when the caller has one (from <c>XPING_LOCAL_STORE</c>, say). A relative
    /// path is read from the repository root or project folder found from
    /// <paramref name="startDirectory"/>. The store never reads the environment itself.
    /// </param>
    /// <returns>A store, which may report <see cref="ILocalSessionStore.IsAvailable"/> as false.</returns>
    public static ILocalSessionStore Create(
        LocalStoreOptions? options = null,
        ILogger? logger = null,
        string? startDirectory = null,
        string? storePath = null) =>
        new JsonSessionStore(
            options ?? new LocalStoreOptions(),
            logger ?? NullLogger.Instance,
            startDirectory,
            storePath);
}
