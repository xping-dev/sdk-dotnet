using System.Diagnostics;
using Microsoft.Net.Http.Headers;
using Microsoft.Playwright;
using Xping.Sdk.Core.Clients.Browser.Internals;
using Xping.Sdk.Shared;

namespace Xping.Sdk.Core.Clients.Browser;

/// <summary>
/// This class represents a client that can interact with a web application using a browser, such as Chromium, Firefox,
/// or WebKit. It uses the Playwright library to create and control the browser instance. Upon instantiation, the 
/// ownership of the Browser and BrowserContext objects is transferred to BrowserResponseMessage class, which then 
/// becomes responsible for managing and releasing the browser resources appropriately.
/// </summary>
[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public class BrowserClient
{
    private readonly IBrowser _browser;
    private IBrowserContext? _context;
    private IPage? _page;

    private readonly BrowserConfiguration _configuration;

    /// <summary>
    /// Returns a browser instance.
    /// </summary>
    public IBrowser Browser => _browser;

    /// <summary>
    /// Initializes a new instance with the specified browser and configuration parameter. A browser 
    /// <see cref="IBrowser"/> object represents the headless browser instance. It can be obtained from the IPlaywright 
    /// interface.
    /// </summary>
    /// <param name="browser">The browser instance.</param>
    /// <param name="configuration">The browser configuration instance.</param>
    public BrowserClient(
        IBrowser browser,
        BrowserConfiguration configuration)
    {
        _browser = browser.RequireNotNull(nameof(browser));
        _configuration = configuration.RequireNotNull(nameof(configuration));
    }

    // Internal access for unit testing purposes.
    internal BrowserClient(
        IBrowser browser,
        IBrowserContext context,
        IPage page,
        BrowserConfiguration configuration)
    {
        _browser = browser.RequireNotNull(nameof(browser));
        _context = context;
        _page = page;
        _configuration = configuration.RequireNotNull(nameof(configuration));
    }

    /// <summary>
    /// A read-only property that gets the name of the headless browser type, such as "chromium", "firefox", or 
    /// "webkit".
    /// </summary>
    public string Name => _browser.BrowserType.Name;

    /// <summary>
    /// A read-only property that gets the version of the headless browser instance.
    /// </summary>
    public string Version => _browser.Version;

    /// <summary>
    /// An asynchronous method that sends an HTTP request to the specified URL and returns a BrowserResponseMessage 
    /// object that represents the response, if the operation completes successfully. The BrowserResponseMessage object 
    /// provides methods and properties to access and manipulate the web page content and functionality.
    /// </summary>
    /// <param name="url">A Uri object that represents the URL of the web page to request.</param>
    /// <param name="handler">
    /// A handler that will be invoked when an HTTP response is received.
    /// </param>
    /// <param name="requestInterceptor">
    /// The interceptor that can modify or handle the request before it is sent.
    /// </param>
    /// <param name="cancellationToken">
    /// An optional CancellationToken object that can be used to cancel this operation. If the operation is cancelled,
    /// the method may return null.
    /// </param>
    /// <returns>
    /// A Task&lt;BrowserResponseMessage?&gt; object that represents the asynchronous operation. The result of the task 
    /// is a <see cref="BrowserResponseMessage"/> object that represents the web page response, or null if the operation 
    /// is cancelled before completion.
    /// </returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Usage",
        "CA2000:Dispose objects before losing scope",
        Justification = "Ownership of browser and its context is transferred to the BrowserResponseMessage.")]
    public async Task<BrowserResponseMessage?> SendAsync(
        Uri url,
        IHttpResponseHandler handler,
        IHttpRequestInterceptor requestInterceptor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url, nameof(url));
        ArgumentNullException.ThrowIfNull(handler, nameof(handler));
        ArgumentNullException.ThrowIfNull(requestInterceptor, nameof(requestInterceptor));

        try
        {
            return await WithCancellationAsync(
                    SendInternalAsync(url, handler, requestInterceptor), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>
    /// An asynchronous method that sends an HTTP request to the specified URL and returns a BrowserResponseMessage 
    /// object that represents the response, if the operation completes successfully. The BrowserResponseMessage object 
    /// provides methods and properties to access and manipulate the web page content and functionality.
    /// </summary>
    /// <param name="url">A Uri object that represents the URL of the web page to request.</param>
    /// <param name="cancellationToken">
    /// An optional CancellationToken object that can be used to cancel this operation. If the operation is cancelled,
    /// the method may return null.
    /// </param>
    /// <returns>
    /// A Task&lt;BrowserResponseMessage?&gt; object that represents the asynchronous operation. The result of the task 
    /// is a <see cref="BrowserResponseMessage"/> object that represents the web page response, or null if the operation 
    /// is cancelled before completion.
    /// </returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Usage",
        "CA2000:Dispose objects before losing scope",
        Justification = "Ownership of browser and its context is transferred to the BrowserResponseMessage.")]
    public async Task<BrowserResponseMessage?> SendAsync(
        Uri url,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url, nameof(url));

        try
        {
            return await WithCancellationAsync(
                    SendInternalAsync(url, handler: null, requestInterceptor: null), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>
    /// An asynchronous method that sends an HTTP request to the specified URL and returns a BrowserResponseMessage 
    /// object that represents the response, if the operation completes successfully. The BrowserResponseMessage object 
    /// provides methods and properties to access and manipulate the web page content and functionality.
    /// </summary>
    /// <param name="url">A Uri object that represents the URL of the web page to request.</param>
    /// <param name="requestInterceptor">
    /// The interceptor that can modify or handle the request before it is sent.
    /// </param>
    /// <param name="cancellationToken">
    /// An optional CancellationToken object that can be used to cancel this operation. If the operation is cancelled,
    /// the method may return null.
    /// </param>
    /// <returns>
    /// A Task&lt;BrowserResponseMessage?&gt; object that represents the asynchronous operation. The result of the task 
    /// is a <see cref="BrowserResponseMessage"/> object that represents the web page response, or null if the operation 
    /// is cancelled before completion.
    /// </returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Usage",
        "CA2000:Dispose objects before losing scope",
        Justification = "Ownership of browser and its context is transferred to the BrowserResponseMessage.")]
    public async Task<BrowserResponseMessage?> SendAsync(
        Uri url,
        IHttpRequestInterceptor requestInterceptor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url, nameof(url));
        ArgumentNullException.ThrowIfNull(requestInterceptor, nameof(requestInterceptor));

        try
        {
            return await WithCancellationAsync(
                    SendInternalAsync(url, handler: null, requestInterceptor), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>
    /// An asynchronous method that sends an HTTP request to the specified URL and returns a BrowserResponseMessage 
    /// object that represents the response, if the operation completes successfully. The BrowserResponseMessage object 
    /// provides methods and properties to access and manipulate the web page content and functionality.
    /// </summary>
    /// <param name="url">A Uri object that represents the URL of the web page to request.</param>
    /// <param name="handler">
    /// A handler that will be invoked when an HTTP response is received.
    /// </param>
    /// <param name="cancellationToken">
    /// An optional CancellationToken object that can be used to cancel this operation. If the operation is cancelled,
    /// the method may return null.
    /// </param>
    /// <returns>
    /// A Task&lt;BrowserResponseMessage?&gt; object that represents the asynchronous operation. The result of the task 
    /// is a <see cref="BrowserResponseMessage"/> object that represents the web page response, or null if the operation 
    /// is cancelled before completion.
    /// </returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Usage",
        "CA2000:Dispose objects before losing scope",
        Justification = "Ownership of browser and its context is transferred to the BrowserResponseMessage.")]
    public async Task<BrowserResponseMessage?> SendAsync(
        Uri url,
        IHttpResponseHandler handler,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url, nameof(url));
        ArgumentNullException.ThrowIfNull(handler, nameof(handler));

        try
        {
            return await WithCancellationAsync(
                    SendInternalAsync(url, handler, requestInterceptor: null), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    private async Task<BrowserResponseMessage> SendInternalAsync(
        Uri url, 
        IHttpResponseHandler? handler,
        IHttpRequestInterceptor? requestInterceptor)
    {
        var shouldClose = ShouldCloseBeforeNewPage();
        
        if (shouldClose)
        {
            await CloseContextAsync().ConfigureAwait(false);
            await ClosePageAsync().ConfigureAwait(false);
        }
        
        _page = await NewBrowserContextWithPageAsync().ConfigureAwait(false);

        if (handler != null)
        {
            // Listen for all responses and handle them appropriately, e.g., handle all HTTP redirections.
            _page.Response += handler.HandleResponse;
        }

        if (requestInterceptor != null)
        {
            // Intercept network requests and modify them before they are sent.
            await _page.RouteAsync("**/*", requestInterceptor.HandleAsync).ConfigureAwait(false);
        }

        var response = await _page.GotoAsync(
                url: url.AbsoluteUri,
                options: new PageGotoOptions { 
                    Timeout = (float)_configuration.HttpRequestTimeout.TotalMilliseconds,
                    WaitUntil = _configuration.WaitUntil,
                })
            .ConfigureAwait(false);

        var responseMessage = await new BrowserResponseMessageBuilder()
            .Build(_browser)
            .Build(_context)
            .Build(_page)
            .Build(response)
            .Build()
            .ConfigureAwait(false);

        return responseMessage;
    }

    /// <summary>
    /// Wraps a non-cancellable task with cancellation support, allowing the caller to stop waiting for the task
    /// when a cancellation token is cancelled. Note that this does not stop the execution of the original task;
    /// the original task will continue to run in the background.
    /// </summary>
    /// <typeparam name="T">The type of the result produced by the task.</typeparam>
    /// <param name="task">The original non-cancellable task.</param>
    /// <param name="cancellationToken">The cancellation token to observe.</param>
    /// <returns>
    /// A task that represents the completion of both the original task and the cancellation observer.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the cancellation token is cancelled before the original task completes.
    /// </exception>
    private static async Task<T> WithCancellationAsync<T>(Task<T> task, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<bool>();
        await using (cancellationToken.Register(s => (s as TaskCompletionSource<bool>)?.TrySetResult(true), tcs))
        {
            // Task.WhenAny is used to await either the completion of the original task or the cancellation task.
            // If the cancellation task completes first, an OperationCanceledException is thrown.
            if (task != await Task.WhenAny(task, tcs.Task).ConfigureAwait(false))
            {
                throw new OperationCanceledException(cancellationToken);
            }
        }

        return await task.ConfigureAwait(false); // task is already completed at this point
    }

    private bool ShouldCloseBeforeNewPage()
    {
        bool result = _page != null || _context != null;

        return result;
    }

    private async Task CloseContextAsync()
    {
        if (_context != null)
        {
            await _context.CloseAsync().ConfigureAwait(false);
        }
    }

    private async Task ClosePageAsync()
    {
        if (_page != null)
        {
            await _page.CloseAsync().ConfigureAwait(false);
        }
    }

    private async Task<IPage> NewBrowserContextWithPageAsync()
    {
        var options = CreateBrowserNewContextOptions();
        _context = await _browser.NewContextAsync(options).ConfigureAwait(false);
        
        // TODO: Investigate _context.Tracing being null
        // Start tracing
        // await _context.Tracing.StartAsync(new TracingStartOptions
        // {
        //     Screenshots = false,
        //     Snapshots = true,
        //     Sources = true,
        // }).ConfigureAwait(false);
        
        _page = await _context.NewPageAsync().ConfigureAwait(false);

        return _page;
    }

    private static IEnumerable<KeyValuePair<string, string>> ConvertDictionary(
        IDictionary<string, IEnumerable<string>> dictionary)
    {
        foreach (var pair in dictionary)
        {
            foreach (var value in pair.Value)
            {
                yield return new KeyValuePair<string, string>(pair.Key, value);
            }
        }
    }

    private BrowserNewContextOptions CreateBrowserNewContextOptions()
    {
        _configuration.GetHttpRequestHeaders().TryGetValue(HeaderNames.UserAgent, out var userAgentHeader);

        var options = new BrowserNewContextOptions
        {
            UserAgent = userAgentHeader?.FirstOrDefault(),
            ExtraHTTPHeaders = ConvertDictionary(FilterHeaders(
                _configuration.GetHttpRequestHeaders(), except: HeaderNames.UserAgent)),
            Geolocation = _configuration.GetGeolocation(),
            Locale = _configuration.Locale
        };

        return options;
    }

    private static Dictionary<string, IEnumerable<string>> FilterHeaders(
        IDictionary<string, IEnumerable<string>> headers, string except)
    {
        // Exclude the `except` header from the dictionary
        var filteredHeaders = headers
            .Where(header => !header.Key.Equals(except, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(header => header.Key, header => header.Value);

        return filteredHeaders;
    }

    private string GetDebuggerDisplay() => Name;
}
