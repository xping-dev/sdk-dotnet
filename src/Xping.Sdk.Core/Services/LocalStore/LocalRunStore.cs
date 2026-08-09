/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xping.Sdk.Core.Services.LocalStore.Internals;

namespace Xping.Sdk.Core.Services.LocalStore;

/// <summary>
/// Creates <see cref="ILocalRunStore"/> instances over the on-disk local run store.
/// </summary>
/// <remarks>
/// This is the supported entry point for reading the store from outside the SDK — the Xping CLI uses
/// it to render reports. The concrete implementation stays internal so its file layout and retention
/// behaviour can change without breaking callers.
/// </remarks>
public static class LocalRunStore
{
    /// <summary>
    /// Creates a store over the default location for the current repository.
    /// </summary>
    /// <param name="options">Retention settings. Defaults are used when <see langword="null"/>.</param>
    /// <param name="logger">Diagnostics sink. Storage problems are logged here at debug level.</param>
    /// <param name="startDirectory">
    /// Directory to begin repository-root discovery from. Defaults to the entry assembly's location.
    /// </param>
    /// <returns>A store, which may report <see cref="ILocalRunStore.IsAvailable"/> as false.</returns>
    public static ILocalRunStore Create(
        LocalStoreOptions? options = null,
        ILogger? logger = null,
        string? startDirectory = null) =>
        new JsonLinesRunStore(
            options ?? new LocalStoreOptions(),
            logger ?? NullLogger.Instance,
            startDirectory);
}
