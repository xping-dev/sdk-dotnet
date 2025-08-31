/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using Microsoft.Extensions.DependencyInjection;
using Xping.Sdk.Core.Common;
using Xping.Sdk.Core.Components;
using Xping.Sdk.Core.Extensions;
using Xping.Sdk.Core.Session;
using Xping.Sdk.Shared;
using Xping.Sdk.Actions.Internals;
using Xping.Sdk.Core.Clients.Http;
using Xping.Sdk.Core.DependencyInjection;
using Xping.Sdk.Core.Services;

namespace Xping.Sdk.Actions;

/// <summary>
/// The HttpClientRequestSender class is a concrete implementation of the <see cref="TestComponent"/> class that is used 
/// to send an HTTP request. It uses the <see cref="IHttpClientFactory"/> to create an instance of the 
/// <see cref="HttpClient"/> class, which is used to send the HTTP request.
/// </summary>
/// <remarks>
/// Before using this test component, you need to register the necessary services by calling the AddHttpClientFactory()
/// method.
/// </remarks>
public sealed class HttpClientRequestSender : TestComponent
{
    private readonly OrderedUrlRedirections _urlRedirections = [];
    private readonly HttpClientConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the HttpClientRequestSender class with the specified configuration.
    /// </summary>
    /// <param name="configuration">The HttpClient configuration.</param>
    public HttpClientRequestSender(HttpClientConfiguration configuration) :
        base(
        type: TestStepType.ActionStep,
        description: "Send HTTP request to target URL",
        displayName: "HTTP Client Request Sender")
    {
        _configuration = configuration.RequireNotNull(nameof(configuration));
    }

    /// <summary>
    /// This method performs the test step operation asynchronously.
    /// </summary>
    /// <param name="url">A Uri object that represents the URL of the page being validated.</param>
    /// <param name="settings">A <see cref="TestSettings"/> object that contains the settings for the test.</param>
    /// <param name="context">A <see cref="TestContext"/> object that represents the test context.</param>
    /// <param name="serviceProvider">An instance object of a mechanism for retrieving a service object.</param>
    /// <param name="cancellationToken">An optional CancellationToken object that can be used to cancel this 
    /// operation.</param>
    /// <returns><see cref="TestStep"/> object.</returns>
    public override async Task HandleAsync(
        Uri url,
        TestSettings settings,
        TestContext context,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url, nameof(url));
        ArgumentNullException.ThrowIfNull(settings, nameof(settings));
        ArgumentNullException.ThrowIfNull(context, nameof(context));

        _urlRedirections.Clear();

        var httpClientFactory = GetHttpClientFactory(serviceProvider);
        using var httpClient = CreateHttpClient(httpClientFactory, serviceProvider);
        using var request = CreateHttpRequestMessage(url);

        await AnnotateTestStepWithHttpRequest(context, request, cancellationToken).ConfigureAwait(false);

        TestStep? testStep = null;
        try
        {
            var response = await SendRequestAsync(httpClient, request, context, cancellationToken)
                .ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
            {
                testStep = context.SessionBuilder.Build(Errors.TestComponentTimeout(this, settings.Timeout));
                return;
            }

            testStep = await BuildTestStepFromHttpResponse(context, response, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            testStep = context.SessionBuilder.Build(exception);
        }
        finally
        {
            if (testStep != null)
            {
                context.Progress?.Report(testStep);
            }
        }
    }

    private static IHttpClientFactory GetHttpClientFactory(IServiceProvider serviceProvider)
    {
        // Check if a keyed service for in-memory web application exists and use it if available
        var keyedFactory = serviceProvider.GetKeyedService<IHttpClientFactory>(
            HttpClientFactoryConfiguration.HttpClientFactoryForWebApplication);
        if (keyedFactory != null)
        {
            return keyedFactory;
        }

        // Fallback to the default IHttpClientFactory
        return serviceProvider.GetService<IHttpClientFactory>() ??
            throw new InvalidProgramException(Errors.HttpClientsNotFound);
    }

    private async Task AnnotateTestStepWithHttpRequest(TestContext context, HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var requestContent = request.Content != null
            ? await ReadAsByteArrayAsync(request.Content, cancellationToken).ConfigureAwait(false)
            : [];

        context.SessionBuilder
            .Build(PropertyBagKeys.HttpRequestHeaders, GetHeaders(request.Headers))
            .Build(PropertyBagKeys.HttpMethod, new PropertyBagValue<string>(request.Method.ToString()))
            .Build(PropertyBagKeys.HttpRequestContent, new PropertyBagValue<byte[]>(requestContent))
            .Build(PropertyBagKeys.HttpFollowRedirect,
                new PropertyBagValue<bool>(_configuration.FollowHttpRedirectionResponses))
            .Build(PropertyBagKeys.MaxRedirections, new PropertyBagValue<int>(_configuration.MaxRedirections))
            .Build(PropertyBagKeys.HttpRetry,
                new PropertyBagValue<bool>(_configuration.RetryHttpRequestWhenFailed))
            .Build(PropertyBagKeys.HttpRequestTimeout,
                new PropertyBagValue<TimeSpan>(_configuration.HttpRequestTimeout));
    }

    private static async Task<TestStep> BuildTestStepFromHttpResponse(TestContext context, HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var buffer = await ReadAsByteArrayAsync(response.Content, cancellationToken).ConfigureAwait(false);

        return context.SessionBuilder
            .Build(PropertyBagKeys.HttpResponseStatus, new PropertyBagValue<string>($"{(int)response.StatusCode}"))
            .Build(PropertyBagKeys.HttpResponseVersion, new PropertyBagValue<string>($"{response.Version}"))
            .Build(PropertyBagKeys.HttpResponsePhrase, new PropertyBagValue<string?>(response.ReasonPhrase))
            .Build(PropertyBagKeys.HttpResponseHeaders, GetHeaders(response.Headers))
            .Build(PropertyBagKeys.HttpResponseTrailingHeaders, GetHeaders(response.TrailingHeaders))
            .Build(PropertyBagKeys.HttpContentHeaders, GetHeaders(response.Content.Headers))
            .Build(PropertyBagKeys.HttpResponseContent, new PropertyBagValue<byte[]>(buffer))
            .Build(PropertyBagKeys.HttpResponseMessage, new NonSerializable<HttpResponseMessage>(response))
            .Build();
    }

    private static PropertyBagValue<Dictionary<string, string>> GetHeaders(HttpHeaders headers) =>
        new(headers.ToDictionary(h => h.Key.ToUpperInvariant(), h => string.Join(";", h.Value)));

    private static async Task<byte[]> ReadAsByteArrayAsync(HttpContent httpContent, CancellationToken cancellationToken)
    {
        // When storing the server response content, it is generally recommended to store it as a byte array
        // rather than a string. This is because the response content may contain binary data that cannot be represented
        // as a string.

        // If you need to convert the byte array to a string for display purposes, you can use the Encoding class to
        // specify the character encoding to use.
        return await httpContent.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    }

    private HttpClient CreateHttpClient(IHttpClientFactory httpClientFactory, IServiceProvider serviceProvider)
    {
        // Check if certificate capture is enabled through the scoped service
        var sslCaptureService = serviceProvider.GetService<ISslCaptureConfigurationService>();
        if (sslCaptureService?.SslCertificateValidationCallback is { } callback)
        {
            // Create a custom HttpClient with certificate capture enabled
            return CreateHttpClientWithCertificateCapture(callback);
        }

        // Use the standard factory-created HttpClient
        HttpClient httpClient = null!;

        if (_configuration.RetryHttpRequestWhenFailed == true)
        {
            httpClient = httpClientFactory.CreateClient(HttpClientFactoryConfiguration.HttpClientWithRetryPolicy);
        }
        else if (_configuration.RetryHttpRequestWhenFailed == false)
        {
            httpClient = httpClientFactory.CreateClient(HttpClientFactoryConfiguration.HttpClientWithNoRetryPolicy);
        }

        httpClient.Timeout = _configuration.HttpRequestTimeout;

        return httpClient;
    }

    private HttpClient CreateHttpClientWithCertificateCapture(RemoteCertificateValidationCallback callback)
    {
#pragma warning disable CA2000 // Handler is disposed by HttpClient when disposeHandler is true
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(15),
            AutomaticDecompression = DecompressionMethods.All,
            AllowAutoRedirect = _configuration.FollowHttpRedirectionResponses,
            UseCookies = false,
            SslOptions = new System.Net.Security.SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = callback
            }
        };

        var httpClient = new HttpClient(handler, disposeHandler: true)
        {
            Timeout = _configuration.HttpRequestTimeout
        };
#pragma warning restore CA2000

        return httpClient;
    }

    private HttpRequestMessage CreateHttpRequestMessage(Uri url)
    {
        var request = new HttpRequestMessage()
        {
            RequestUri = url,
            Method = _configuration.GetHttpMethod(),
            Content = _configuration.GetHttpContent()
        };

        // Clear any existing headers from the content to prevent unintended data from being sent.
        request.Content?.Headers.Clear();
        // Clear any existing headers from the request itself for the same reason.
        request.Headers.Clear();

        // This ensures that only the headers explicitly set by the user will be included in the request.

        foreach (var httpHeader in _configuration.GetHttpRequestHeaders())
        {
            if (request.Content != null &&
                httpHeader.Key.ToUpperInvariant().StartsWith("CONTENT", StringComparison.InvariantCulture))
            {
                request.Content.Headers.Add(httpHeader.Key, httpHeader.Value);
            }
            else
            {
                request.Headers.Add(httpHeader.Key, httpHeader.Value);
            }
        }

        return request;
    }

    private async Task<HttpResponseMessage> SendRequestAsync(
        HttpClient httpClient,
        HttpRequestMessage request,
        TestContext context,
        CancellationToken cancellationToken)
    {
        if (_configuration.FollowHttpRedirectionResponses)
        {
            if (request.RequestUri != null)
            {
                _urlRedirections.Add(request.RequestUri.AbsoluteUri);
            }

            using var instrumentation = new InstrumentationTimer(startStopwatch: true);

            var response = await httpClient
                .SendAsync(request, cancellationToken)
                .ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
            {
                return response;
            }

            while (IsRedirection(response) && _urlRedirections.Count <= _configuration.MaxRedirections)
            {
                TestStep testStep = context.SessionBuilder
                    .Build(PropertyBagKeys.HttpResponseStatus,
                        new PropertyBagValue<string>($"{(int)response.StatusCode}"))
                    .Build(PropertyBagKeys.HttpResponseVersion, new PropertyBagValue<string>($"{response.Version}"))
                    .Build(PropertyBagKeys.HttpResponsePhrase, new PropertyBagValue<string?>(response.ReasonPhrase))
                    .Build(PropertyBagKeys.HttpResponseHeaders, GetHeaders(response.Headers))
                    .Build(PropertyBagKeys.HttpResponseTrailingHeaders, GetHeaders(response.TrailingHeaders))
                    .Build(PropertyBagKeys.HttpContentHeaders, GetHeaders(response.Content.Headers))
                    .Build();
                context.Progress?.Report(testStep);

                // Location HTTP header specifies the absolute or relative URL of the new resource.
                Uri? redirectUrl = response.Headers.Location;

                if (redirectUrl != null)
                {
                    if (!redirectUrl.IsAbsoluteUri)
                    {
                        string lastAbsoluteUri =
                            _urlRedirections.FindLastMatchingItem(url => new Uri(url).IsAbsoluteUri) ??
                            throw new InvalidOperationException(
                                "Invalid Redirection Attempt Detected. The server " +
                                "attempted to redirect to an invalid or unrecognized location. Please check the URL " +
                                "or contact the site administrator for assistance.");

                        redirectUrl = new Uri(baseUri: new Uri(lastAbsoluteUri), relativeUri: redirectUrl);
                    }

                    if (_urlRedirections.Add(redirectUrl.ToString()) == false)
                    {
                        // Circular dependency detected
                        throw new InvalidOperationException(
                            $"A circular dependency was detected for the URL {redirectUrl}. " +
                            $"The redirection chain is: {string.Join(" -> ", _urlRedirections)}");
                    }

                    using HttpRequestMessage redirectRequest = CreateHttpRequestMessage(redirectUrl);

                    response = await httpClient
                        .SendAsync(redirectRequest, cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            // Throw an exception if the max number of redirects has been reached
            if (_urlRedirections.Count > _configuration.MaxRedirections)
            {
                throw new WebException(
                    $"The maximum number of redirects ({_configuration.MaxRedirections}) has been exceeded for the " +
                    $"URL {request.RequestUri}. The last redirect URL was " +
                    $"{_urlRedirections.FindLastMatchingItem(str => !string.IsNullOrEmpty(str))}.",
                    WebExceptionStatus.ProtocolError);
            }

            return response;
        }
        else
        {
            return await httpClient
                .SendAsync(request, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static bool IsRedirection(HttpResponseMessage response)
    {
        // It is not recommended to handle redirects by checking if the HTTP status code is between 300 and 399
        // because it does not account for the different types of redirections and their implications. For
        // example, some redirects may change the request method from POST to GET or require user confirmation
        // before proceeding. Therefore, it is better to use the StatusCode property of the HttpResponseMessage
        // class, which returns a value of the HttpStatusCode enum. This way, we can handle each redirection
        // type appropriately and follow the best practices.

        return response.StatusCode == HttpStatusCode.Redirect ||
               response.StatusCode == HttpStatusCode.MovedPermanently ||
               response.StatusCode == HttpStatusCode.Found ||
               response.StatusCode == HttpStatusCode.SeeOther ||
               response.StatusCode == HttpStatusCode.TemporaryRedirect;
    }
}