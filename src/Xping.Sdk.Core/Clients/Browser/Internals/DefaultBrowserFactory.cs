/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using Microsoft.Playwright;
using Xping.Sdk.Core.Components;

namespace Xping.Sdk.Core.Clients.Browser.Internals;

// This class is being used in DependencyInjection and as such shows as never instantiated thus disabling the warning.

#pragma warning disable CA1812 // internal class that is apparently never instantiated
internal class DefaultBrowserFactory : IBrowserFactory
#pragma warning restore CA1812 // internal class that is apparently never instantiated
{
    private bool _disposedValue;
    private IPlaywright? _playwright;

    public async Task<BrowserClient> CreateClientAsync(BrowserConfiguration configuration, TestSettings settings)
    {
        ArgumentNullException.ThrowIfNull(configuration, nameof(configuration));

        _playwright ??= await CreatePlaywrightAsync().ConfigureAwait(false);
        _playwright.Selectors.SetTestIdAttribute(settings.TestIdAttribute);

        var launchOptions = new BrowserTypeLaunchOptions
        {
            Headless = configuration.Headless,
        };

        return configuration.BrowserType switch
        {
            BrowserType.Webkit =>
                new BrowserClient(
                    browser: await _playwright.Webkit.LaunchAsync(launchOptions).ConfigureAwait(false), configuration),

            BrowserType.Firefox =>
                new BrowserClient(
                    browser: await _playwright.Firefox.LaunchAsync(launchOptions).ConfigureAwait(false), configuration),

            _ => new BrowserClient(
                browser: await _playwright.Chromium.LaunchAsync(launchOptions).ConfigureAwait(false), configuration),
        };
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual async Task<IPlaywright> CreatePlaywrightAsync()
    {
        return await Playwright.CreateAsync().ConfigureAwait(false);
    }

    private void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _playwright?.Dispose();
            }

            _playwright = null;
            _disposedValue = true;
        }
    }
}
