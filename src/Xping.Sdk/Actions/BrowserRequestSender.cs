/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using Xping.Sdk.Core.Common;
using Xping.Sdk.Core.Components;
using Xping.Sdk.Core.Session;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;
using Xping.Sdk.Core.Clients.Browser;
using Xping.Sdk.Shared;
using Xping.Sdk.Actions.Internals;

namespace Xping.Sdk.Actions;

/// <summary>
/// The BrowserRequestSender class is a subclass of the TestComponent abstract class that implements the 
/// ITestComponent interface. It is used to send HTTP requests to a web application using a headless browserClient, 
/// such as Chromium, Firefox, or WebKit. It uses the Playwright library to create and control the headless 
/// browserClient instance. 
/// </summary>
/// <remarks>
/// Before using this test component, you need to register the necessary services by calling the 
/// <see cref="Core.DependencyInjection.DependencyInjectionExtension.AddBrowserClientFactory(IServiceCollection)"/> 
/// method which adds <see cref="IBrowserFactory"/> factory service. The Xping SDK provides a default implementation 
/// of this interface, called DefaultBrowserFactory, which based on the <see cref="BrowserConfiguration"/> creates a 
/// Chromium, WebKit, or Firefox headless browserClient instance. You can also implement your own custom headless 
/// browserClient factory by implementing the <see cref="IBrowserFactory"/> interface and adding its implementation into
/// services.
/// </remarks>
public sealed class BrowserRequestSender : TestComponent
{
    private readonly BrowserConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the BrowserRequestSender class with the specified bowser configuration.
    /// </summary>
    /// <param name="configuration">The browserClient configuration.</param>
    public BrowserRequestSender(BrowserConfiguration configuration) : base(
        name: nameof(BrowserRequestSender),
        type: TestStepType.ActionStep,
        description: "Navigate to target URL using headless browser")
    {
        _configuration = configuration.RequireNotNull(nameof(configuration));
    }

    /// <summary>
    /// This method performs the test step operation asynchronously.
    /// </summary>
    /// <param name="url">A Uri object that represents the URL of the page being validated.</param>
    /// <param name="settings">A <see cref="TestSettings"/> object that contains the settings for the test.</param>
    /// <param name="context">A <see cref="TestContext"/> object that represents the test session.</param>
    /// <param name="serviceProvider">An instance object of a mechanism for retrieving a service object.</param>
    /// <param name="cancellationToken">
    /// An optional CancellationToken object that can be used to cancel this operation.
    /// </param>
    /// <returns><see cref="TestStep"/> object.</returns>
    public override async Task HandleAsync(
        Uri url,
        TestSettings settings,
        TestContext context,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url, nameof(url));
        ArgumentNullException.ThrowIfNull(context, nameof(context));
        ArgumentNullException.ThrowIfNull(settings, nameof(settings));

        TestStep? testStep = null;
        try
        {
            var browserClient = await CreateBrowserClientAsync(serviceProvider, settings).ConfigureAwait(false);
            await AnnotateTestStepWithBrowserRequest(context, browserClient, cancellationToken).ConfigureAwait(false);
            var responseMessage =
                await SendRequestAsync(url, browserClient, context, cancellationToken).ConfigureAwait(false);

            testStep = await HandleResponseAsync(responseMessage, context, settings, cancellationToken)
                .ConfigureAwait(false);
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

    private async Task<BrowserClient> CreateBrowserClientAsync(IServiceProvider serviceProvider, TestSettings settings)
    {
        var browserFactory = serviceProvider.GetService<IBrowserFactory>() ??
            throw new InvalidProgramException(Errors.HeadlessBrowserNotFound);

        return await browserFactory
            .CreateClientAsync(_configuration, settings)
            .ConfigureAwait(false);
    }

    private async Task<BrowserResponseMessage?> SendRequestAsync(
        Uri url,
        BrowserClient browserClient,
        TestContext context,
        CancellationToken cancellationToken)
    {
        var urlRedirections = new OrderedUrlRedirections { url.AbsoluteUri };
        var responseHandler = CreateHttpResponseHandler(url, context, urlRedirections);
        var requestInterceptor = CreateHttpRequestInterceptor();

        return await browserClient
            .SendAsync(url, responseHandler, requestInterceptor, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<TestStep> HandleResponseAsync(
        BrowserResponseMessage? responseMessage,
        TestContext context,
        TestSettings settings,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested || responseMessage == null)
        {
            return context.SessionBuilder.Build(Errors.TestComponentTimeout(this, settings.Timeout));
        }

        var buffer = await ReadAsByteArrayAsync(responseMessage.HttpResponseMessage.Content, cancellationToken)
            .ConfigureAwait(false);

        return BuildTestStepFromBrowserResponse(responseMessage, buffer, context);
    }

    private async Task AnnotateTestStepWithBrowserRequest(
        TestContext context,
        BrowserClient browserClient,
        CancellationToken cancellationToken)
    {
        var httpContent = _configuration.GetHttpContent();
        var requestContent = httpContent != null
            ? await httpContent.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false)
            : [];

        context.SessionBuilder
            .Build(PropertyBagKeys.HttpRequestHeaders,
                new PropertyBagValue<Dictionary<string, string>>(_configuration
                    .GetHttpRequestHeaders()
                    .ToDictionary(h => h.Key.ToUpperInvariant(), h => string.Join(";", h.Value))))
            .Build(PropertyBagKeys.HttpMethod, new PropertyBagValue<string>(_configuration.GetHttpMethod().Method))
            .Build(PropertyBagKeys.HttpRequestContent, new PropertyBagValue<byte[]>(requestContent))
            .Build(PropertyBagKeys.HttpFollowRedirect,
                new PropertyBagValue<bool>(_configuration.FollowHttpRedirectionResponses))
            .Build(PropertyBagKeys.MaxRedirections, new PropertyBagValue<int>(_configuration.MaxRedirections))
            .Build(PropertyBagKeys.HttpRequestTimeout,
                new PropertyBagValue<TimeSpan>(_configuration.HttpRequestTimeout))
            .Build(PropertyBagKeys.BrowserName, new PropertyBagValue<string>(browserClient.Name))
            .Build(PropertyBagKeys.BrowserVersion, new PropertyBagValue<string>(browserClient.Version));

        if (_configuration.Headless.HasValue)
        {
            context.SessionBuilder
                .Build(PropertyBagKeys.BrowserHeadless, new PropertyBagValue<bool>(_configuration.Headless.Value));
        }
    }

    private static TestStep BuildTestStepFromBrowserResponse(
        BrowserResponseMessage responseMessage,
        byte[] buffer,
        TestContext context)
    {
        var response = responseMessage.HttpResponseMessage;

        return context.SessionBuilder
            .Build(PropertyBagKeys.HttpResponseStatus, new PropertyBagValue<string>($"{(int)response.StatusCode}"))
            .Build(PropertyBagKeys.HttpResponseVersion, new PropertyBagValue<string>($"{response.Version}"))
            .Build(PropertyBagKeys.HttpResponsePhrase, new PropertyBagValue<string?>(response.ReasonPhrase))
            .Build(PropertyBagKeys.HttpResponseHeaders, GetHeaders(response.Headers))
            .Build(PropertyBagKeys.HttpResponseTrailingHeaders, GetHeaders(response.TrailingHeaders))
            .Build(PropertyBagKeys.HttpContentHeaders, GetHeaders(response.Content.Headers))
            .Build(PropertyBagKeys.HttpResponseContent, new PropertyBagValue<byte[]>(buffer))
            .Build(PropertyBagKeys.BrowserResponseMessage, new NonSerializable<BrowserResponseMessage>(responseMessage))
            .Build(PropertyBagKeys.HttpResponseMessage, new NonSerializable<HttpResponseMessage>(response))
            .Build();
    }

    private BrowserRedirectionResponseHandler CreateHttpResponseHandler(
        Uri url,
        TestContext context,
        OrderedUrlRedirections urlRedirections) => new(
        url,
        context,
        urlRedirections,
        _configuration.MaxRedirections);

    private BrowserHttpRequestInterceptor CreateHttpRequestInterceptor() => new(_configuration);

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
}