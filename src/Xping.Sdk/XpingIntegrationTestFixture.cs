using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Xping.Sdk.Core.DependencyInjection;

namespace Xping.Sdk;

/// <summary>
/// Provides a base test fixture class for integration testing within the Xping SDK environment.
/// </summary>
/// <typeparam name="TEntryPoint">
/// The entry point class of the application under test, typically the Startup class.
/// </typeparam>
/// <remarks>
/// This class is designed to streamline the setup and configuration of the Xping SDK for integration testing purposes.
/// </remarks>
public class XpingIntegrationTestFixture<TEntryPoint> : XpingTestFixture where TEntryPoint : class
{
    private WebApplicationFactory<TEntryPoint>? _webAppFactory;

    /// <summary>
    /// Gives a fixture an opportunity to configure the host before it gets built.
    /// </summary>
    /// <param name="builder">The <see cref="IHostBuilder"/> for the host.</param>
    protected override void ConfigureHost(IHostBuilder builder)
    {
        _webAppFactory = CreateFactory();

        builder.ConfigureServices(services =>
        {
            services.AddTestServerHttpClientFactory(_webAppFactory.CreateClient)
                    .AddTestAgent();
        });
    }

    /// <summary>
    /// Instantiates a new WebApplicationFactory of type TEntryPoint.
    /// </summary>
    /// <remarks>
    /// This method creates a new instance of the WebApplicationFactory class, which provides a test server for the 
    /// application. TEntryPoint is the entry point of the application, typically the Startup class, which configures 
    /// services. This method can be overridden in derived class to customize the instantiation of the 
    /// WebApplicationFactory.
    /// </remarks>
    /// <returns>
    /// A new instance of WebApplicationFactory configured for integration testing.
    /// </returns>
    protected virtual WebApplicationFactory<TEntryPoint> CreateFactory()
    {
        return new WebApplicationFactory<TEntryPoint>();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="disposing"></param>
    protected override void Dispose(bool disposing)
    {
        _webAppFactory?.Dispose();

        base.Dispose(disposing);
    }
}
