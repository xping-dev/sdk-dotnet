/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.Logging;
using Xping.Sdk.Core.Models.Local;

namespace Xping.Sdk.Core.Services.LocalStore.Internals;

/// <summary>
/// Default <see cref="ILocalRunWriter"/>: persists the run and swallows storage failures.
/// </summary>
internal sealed class LocalRunWriter(
    ILocalRunStore store,
    ILogger<LocalRunWriter> logger) : ILocalRunWriter
{
    public bool Write(LocalRun run)
    {
        if (run == null)
            throw new ArgumentNullException(nameof(run));

        try
        {
            return store.Write(run);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Local history is a side channel; it must never be the reason a test run fails.
            logger.LogDebug("Local run not stored: {Message}", ex.Message);
            return false;
        }
    }
}
