using Xping.Sdk.Core.Components;

namespace Xping.Sdk.Core.Clients.Browser;

/// <summary>
/// This interface defines a method to create a <see cref="BrowserClient"/> object that can interact with a web 
/// application using a headless browser, such as Chromium, Firefox, or WebKit. It also implements the IDisposable 
/// interface to support the disposal of unmanaged resources.
/// </summary>
public interface IBrowserFactory : IDisposable
{
    /// <summary>
    /// An asynchronous method that creates a <see cref="BrowserClient"/> object with the specified browser context 
    /// options.
    /// </summary>
    /// <param name="configuration">
    /// A <see cref="BrowserConfiguration"/> object that represents the browser 
    /// configuration, such as browser type, timeout, and user agent.
    /// </param>
    /// <param name="settings">
    /// A <see cref="TestSettings"/> object that contains various testing parameters and preferences, such as 
    /// TestIdAttribute details, and other custom settings that may influence the behavior of the created 
    /// <see cref="BrowserClient"/>.
    /// </param>
    /// <returns>
    /// A Task&lt;BrowserClient&gt; object that represents the asynchronous operation. The result of the task is 
    /// a <see cref="BrowserClient"/> object that can interact with a web application using a browser.
    /// </returns>
    Task<BrowserClient> CreateClientAsync(BrowserConfiguration configuration, TestSettings settings);
}
