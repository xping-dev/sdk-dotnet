/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Collections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xping.Sdk.Core;
using Xping.Sdk.Core.DependencyInjection;

namespace Xping.Sdk;

/// <summary>
/// Provides a base test fixture class for the Xping SDK environment.
/// </summary>
/// <remarks>
/// This class is designed to streamline the setup and configuration of the Xping SDK for testing purposes.
/// It uses lazy initialization to ensure resources are created efficiently and only when needed.
/// Implements <see cref="IDisposable"/> for proper resource cleanup after testing.
/// 
/// The main purpose of this class is to provide an instance of the <see cref="TestAgent"/> class to the tests,
/// ensuring that all necessary dependencies are available and properly configured.
/// </remarks>
public class XpingTestFixture : IEnumerable, IDisposable
{
    private readonly Lazy<IHost> _host;
    private bool _disposedValue;

    /// <summary>
    /// Creates a new instance of this class.
    /// </summary>
    public XpingTestFixture()
    {
        _host = new(valueFactory: CreateHost);
    }

    /// <summary>
    /// Returns an enumerator that iterates through the test fixture data collection.
    /// </summary>
    /// <remarks>
    /// This method is designed to return an instance of the Xping <see cref="TestAgent"/> for use in tests.
    /// </remarks>
    /// <returns>
    /// An <see cref="IEnumerator"/> object that can be used to iterate through the test fixture data collection.
    /// </returns>
    public virtual IEnumerator GetEnumerator()
    {
        var testAgent = _host.Value.Services.GetRequiredService<TestAgent>();

        yield return new object[] { testAgent };
    }

    /// <summary>
    /// Releases the resources used by this fixture.
    /// </summary>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Gives a fixture an opportunity to configure the host before it gets built.
    /// </summary>
    /// <param name="builder">The <see cref="IHostBuilder"/> for the host.</param>
    protected virtual void ConfigureHost(IHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddBrowserClientFactory()
                    .AddHttpClientFactory()
                    .AddTestAgent();
        });
    }

    /// <summary>
    /// Releases the resources used by the XpingIntegrationTestFixture.
    /// </summary>
    /// <param name="disposing">A flag indicating whether to release the managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing && _host != null && _host.IsValueCreated)
            {
                _host.Value.Dispose();
            }

            _disposedValue = true;
        }
    }

    private IHost CreateHost()
    {
        var builder = Host.CreateDefaultBuilder();
        ConfigureHost(builder);
        var host = builder.Build();

        return host;
    }
}
