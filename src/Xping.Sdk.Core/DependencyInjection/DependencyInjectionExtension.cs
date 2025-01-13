/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Polly;
using Xping.Sdk.Core.Clients.Browser;
using Xping.Sdk.Core.Clients.Browser.Internals;
using Xping.Sdk.Core.Clients.Http;
using Xping.Sdk.Core.Session;

namespace Xping.Sdk.Core.DependencyInjection;

/// <summary>
/// Provides extension methods for the IServiceCollection interface to register test-related services.
/// </summary>
public static class DependencyInjectionExtension
{
    /// <summary>
    /// This extension method adds the IHttpClientFactory service and related services to service collection and 
    /// configures a named <see cref="HttpClient"/> clients.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <param name="configuration">The <see cref="HttpClientFactoryConfiguration"/>.</param>
    /// <returns><see cref="IServiceCollection"/> object.</returns>
    public static IServiceCollection AddHttpClientFactory(
        this IServiceCollection services,
        Action<IServiceProvider, HttpClientFactoryConfiguration>? configuration = null)
    {
        var factoryConfiguration = new HttpClientFactoryConfiguration();
        configuration?.Invoke(services.BuildServiceProvider(), factoryConfiguration);

        services.AddHttpClient(HttpClientFactoryConfiguration.HttpClientWithNoRetryPolicy)
                .ConfigurePrimaryHttpMessageHandler(configureHandler =>
                {
                    return CreateSocketsHttpHandler(factoryConfiguration);
                });

        services.AddHttpClient(HttpClientFactoryConfiguration.HttpClientWithRetryPolicy)
                .ConfigurePrimaryHttpMessageHandler(configureHandler =>
                {
                    return CreateSocketsHttpHandler(factoryConfiguration);
                })
                .AddTransientHttpErrorPolicy(builder => builder.WaitAndRetryAsync(
                    sleepDurations: factoryConfiguration.SleepDurations))
                .AddTransientHttpErrorPolicy(builder => builder.CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: factoryConfiguration.HandledEventsAllowedBeforeBreaking,
                    durationOfBreak: factoryConfiguration.DurationOfBreak));

        return services;
    }

    /// <summary>
    /// Adds an IHttpClientFactory implemented by <see cref="WebApplicationHttpClientFactory"/> to the service 
    /// collection. This factory is specifically designed to align with system requirements for returning HttpClient 
    /// instances produced by WebApplicationFactory.
    /// </summary>
    /// <remarks>
    /// The IHttpClientFactory provides a central location for creating and configuring HttpClient instances.
    /// It ensures efficient reuse of HttpClient instances, manages their lifetime, and allows customization of their 
    /// behavior. The WebApplicationHttpClientFactory is tailored to work seamlessly with WebApplicationFactory and its 
    /// associated services.
    /// </remarks>
    /// <param name="services">The service collection to add the service to.</param>
    /// <param name="createClient">
    /// A function that creates an HttpClient instance, typically WebApplicationFactory.CreateClient method.
    /// </param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddTestServerHttpClientFactory(
        this IServiceCollection services, 
        Func<WebApplicationFactoryClientOptions, HttpClient> createClient)
    {
        var factoryConfiguration = new HttpClientFactoryConfiguration();
        services.TryAddTransient<IHttpClientFactory>(implementationFactory:
            serviceProvider => new WebApplicationHttpClientFactory(createClient, factoryConfiguration));

        return services;
    }

    /// <summary>
    /// This extension method adds the necessary factory service to create headless browser instance into your 
    /// application’s service collection.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <returns><see cref="IServiceCollection"/> object.</returns>
    public static IServiceCollection AddBrowserClientFactory(this IServiceCollection services)
    {
        // Register the DefaultBrowserFactory as a singleton to ensure there is only one instance throughout the
        // application lifecycle. This instance maintains a reference to the IPlaywright library, optimizing its
        // creation and disposal. By registering it as a singleton, we ensure that the browser factory is available
        // on-demand and efficiently manages resources until the application shuts down.
        services.TryAddSingleton<IBrowserFactory, DefaultBrowserFactory>();

        return services;
    }

    /// <summary>
    /// Adds a named test agent to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add the service to.</param>
    /// <param name="name">The name of the test agent.</param>
    /// <param name="builder">A function that configures the test agent instance.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddTestAgent(
        this IServiceCollection services,
        string name,
        Action<TestAgent> builder)
    {
        services.TryAddTransient<ITestSessionBuilder, TestSessionBuilder>();
        services.AddKeyedTransient(
            serviceKey: name,
            implementationFactory: (IServiceProvider provider, object serviceKey) =>
            {
                var agent = new TestAgent(provider);
                builder(agent);

                return agent;
            });

        return services;
    }

    /// <summary>
    /// Adds a test agent to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add the service to.</param>
    /// <param name="builder">A function that configures the test agent instance.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddTestAgent(
        this IServiceCollection services,
        Action<TestAgent> builder)
    {
        services.TryAddTransient<ITestSessionBuilder, TestSessionBuilder>();
        services.AddTransient(
            implementationFactory: (IServiceProvider provider) =>
            {
                var agent = new TestAgent(provider);
                builder(agent);

                return agent;
            });

        return services;
    }

    /// <summary>
    /// Adds a default test agent to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add the service to.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddTestAgent(this IServiceCollection services)
    {
        services.TryAddTransient<ITestSessionBuilder, TestSessionBuilder>();
        services.AddTransient(implementationFactory: (IServiceProvider provider) => new TestAgent(provider));

        return services;
    }

    /// <summary>
    /// Checks if a specific service type is already registered in the service collection.
    /// </summary>
    /// <typeparam name="TService">The type of the service to check for registration.</typeparam>
    /// <param name="services">The IServiceCollection instance to search in.</param>
    /// <returns>
    /// True if the service type is registered; otherwise, false.
    /// </returns>
    public static bool IsServiceRegistered<TService>(this IServiceCollection services)
    {
        return services.Any(serviceDescriptor => serviceDescriptor.ServiceType == typeof(TService));
    }

    private static SocketsHttpHandler CreateSocketsHttpHandler(
        HttpClientFactoryConfiguration factoryConfiguration) => new()
        {
            PooledConnectionLifetime = factoryConfiguration.PooledConnectionLifetime,
            AutomaticDecompression = factoryConfiguration.AutomaticDecompression,
            AllowAutoRedirect = HttpClientFactoryConfiguration.HttpClientAutoRedirects,
            UseCookies = HttpClientFactoryConfiguration.HttpClientHandleCookies,
        };
}
