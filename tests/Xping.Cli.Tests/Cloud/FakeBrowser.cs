/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Net;
using Xping.Cli.Auth.Browser;

namespace Xping.Cli.Tests.Cloud;

/// <summary>
/// The user's browser: visits the authorization URL on <see cref="FakeCloud"/> and follows its
/// redirect to the CLI's listener, as a real browser would.
/// </summary>
internal sealed class FakeBrowser : IBrowserLauncher
{
    private readonly List<Uri> _opened = [];
    private readonly List<(HttpStatusCode Status, string Body)> _callbacks = [];
    private readonly TaskCompletionSource _played = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Lock _gate = new();

    /// <summary>
    /// Gets or sets how many times the authorization URL is visited; a second visit plays the user
    /// trying again after a bad redirect.
    /// </summary>
    public int Visits { get; set; } = 1;

    /// <summary>
    /// Gets or sets whether the browser reports that it opened.
    /// </summary>
    public bool Opens { get; set; } = true;

    /// <summary>
    /// Gets the environment the CLI passed with the last URL.
    /// </summary>
    public BrowserEnvironment? Environment { get; private set; }

    public IReadOnlyList<Uri> Opened
    {
        get
        {
            lock (_gate)
                return [.. _opened];
        }
    }

    /// <summary>
    /// Gets what the CLI's listener answered to each redirect.
    /// </summary>
    public IReadOnlyList<(HttpStatusCode Status, string Body)> Callbacks
    {
        get
        {
            lock (_gate)
                return [.. _callbacks];
        }
    }

    /// <summary>
    /// Gets a task that completes when every visit is done.
    /// </summary>
    public Task Played => _played.Task;

    public bool TryOpen(Uri url, BrowserEnvironment environment)
    {
        lock (_gate)
        {
            _opened.Add(url);
            Environment = environment;
        }

        _ = Task.Run(() => PlayAsync(url));
        return Opens;
    }

    private async Task PlayAsync(Uri url)
    {
        try
        {
            using var handler = new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false };
            using var client = new HttpClient(handler, disposeHandler: false);

            for (int visit = 0; visit < Visits; visit++)
            {
                using HttpResponseMessage consent = await client.GetAsync(url).ConfigureAwait(false);
                if (consent.StatusCode != HttpStatusCode.Redirect)
                    continue;

                using HttpResponseMessage callback = await client.GetAsync(consent.Headers.Location).ConfigureAwait(false);
                string body = await callback.Content.ReadAsStringAsync().ConfigureAwait(false);

                lock (_gate)
                    _callbacks.Add((callback.StatusCode, body));
            }

            _played.TrySetResult();
        }
        catch (Exception ex)
        {
            _played.TrySetException(ex);
        }
    }
}
