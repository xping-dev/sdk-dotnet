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
