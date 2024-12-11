using Microsoft.Playwright;
using Xping.Sdk.Shared;

namespace Xping.Sdk.Core.Clients.Browser;

/// <summary>
/// This class represents a web page response that is obtained and interacted with through a browser, such as Chromium,
/// Firefox, or WebKit. It leverages the HttpResponseMessage class to store the HTTP response message, including the 
/// status code, headers, and content, and utilizes the Page class for page interactions.
/// </summary>
/// <remarks>
/// The BrowserResponseMessage takes ownership of the browser and browser's page and its context. It ensures that all
/// associated resources are properly released when the BrowserResponseMessage instance is disposed. This management of
/// resources allows for the safe and efficient handling of the browser's lifecycle within the scope of the 
/// BrowserResponseMessage's usage. The actual results are then transferred to the <see cref="Session.TestSession"/> 
/// objects into its PropertyBag, maintaining the integrity and state of the session throughout the test lifecycle.
/// </remarks>
public sealed class BrowserResponseMessage : IDisposable, IAsyncDisposable
{
    private HttpResponseMessage _responseMessage;
    private IBrowser _browser;
    private IBrowserContext _context;
    private IPage _page;
    private bool _disposedValue;

    /// <summary>
    /// Initializes new instance of the BrowserResponseMessage object.
    /// </summary>
    /// <param name="responseMessage">An HttpResponseMessage object for the BrowserResponseMessage.</param>
    /// <param name="browser">A IBrowser object.</param>
    /// <param name="context">A IBrowserContext object.</param>
    /// <param name="page">A IPage object.</param>
    public BrowserResponseMessage(
        IBrowser browser, IBrowserContext context, IPage page, HttpResponseMessage responseMessage)
    {
        _responseMessage = responseMessage.RequireNotNull(nameof(responseMessage));
        _browser = browser.RequireNotNull(nameof(browser));
        _context = context.RequireNotNull(nameof(context));
        _page = page.RequireNotNull(nameof(page));
    }

    /// <summary>
    /// A read-only property that gets the <see cref="HttpResponseMessage"/> object that is associated with the page.
    /// </summary>
    public HttpResponseMessage HttpResponseMessage => _responseMessage;

    /// <summary>
    /// A read-only property that gets the browser's page.
    /// </summary>
    public IPage Page => _page;

    /// <summary>
    /// Releases the resources used by the browser client.
    /// </summary>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Asynchronously releases the resources used by the browser client.
    /// </summary>
    /// <returns>A task that represents the asynchronous dispose operation.</returns>
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);

        Dispose(disposing: false);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources used by the headless browser client and optionally releases the managed 
    /// resources.
    /// </summary>
    /// <param name="disposing">A flag indicating whether to release the managed resources.</param>
    private void Dispose(bool disposing)
    {
        if (_disposedValue) 
            return;
        
        if (disposing)
        {
            _responseMessage.Dispose();
            _page.CloseAsync().GetAwaiter().GetResult();
            _context.CloseAsync().GetAwaiter().GetResult();

            var valueTask = _context?.DisposeAsync();
                
            if (valueTask.HasValue && !valueTask.Value.IsCompleted)
            {
                // If the ValueTask is not completed, convert it to a Task and then await it synchronously.
                valueTask.Value.AsTask().GetAwaiter().GetResult();
            }

            valueTask = _browser?.DisposeAsync();

            if (valueTask.HasValue && !valueTask.Value.IsCompleted)
            {
                // If the ValueTask is not completed, convert it to a Task and then await it synchronously.
                valueTask.Value.AsTask().GetAwaiter().GetResult();
            }

            _page = null!;
            _context = null!;
            _browser = null!;
            _responseMessage = null!;
        }

        _disposedValue = true;
    }

    /// <summary>
    /// Asynchronously performs the core logic of disposing the browser client.
    /// </summary>
    /// <returns>A task that represents the asynchronous dispose operation.</returns>
    private async Task DisposeAsyncCore()
    {
        if (_disposedValue)
            return;
        
        await _page.CloseAsync().ConfigureAwait(false);
        await _context.CloseAsync().ConfigureAwait(false);
        await _context.DisposeAsync().ConfigureAwait(false);
        await _browser.DisposeAsync().ConfigureAwait(false);

        // Dispose of any other IDisposable objects.
        _responseMessage?.Dispose();

        _page = null!;
        _context = null!;
        _browser = null!;
        _responseMessage = null!;
    }
}
