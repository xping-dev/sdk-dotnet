/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using Xping.Sdk.Core.Extensions;
using Xping.Sdk.Core;
using Xping.Sdk.Core.Common;
using Xping.Sdk.Core.Components;
using Xping.Sdk.Core.Session;
using Xping.Sdk.IntegrationTests.HttpServer;
using Xping.Sdk.IntegrationTests.TestFixtures;
using System.Net.Http.Json;
using Xping.Sdk.Core.Clients.Browser;
using System.Diagnostics;
using Xping.Sdk.Actions;

namespace Xping.Sdk.IntegrationTests;

[SingleThreaded]
[SetUpFixture]
[TestFixtureSource(typeof(TestFixtureProvider), nameof(TestFixtureProvider.ServiceProvider))]
public class BrowserTestAgentTests(IServiceProvider serviceProvider)
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    [SetUp]
    public void Setup()
    {
        var progressMock = Mock.Get(_serviceProvider.GetRequiredService<IProgress<TestStep>>());
        progressMock?.Reset();
    }

    [Test]
    public async Task TestSessionFromBrowserTestAgentIsMarkedCompletedWhenSucceeded()
    {
        // Arrange
        const TestSessionState expectedTestSessionState = TestSessionState.Completed;

        // Act
        using TestSession session = await GetTestSessionFromInMemoryHttpTestServer(
                responseBuilder: SimplePageResponseBuilder)
            .ConfigureAwait(false);

        // Assert
        Assert.That(session.State, Is.EqualTo(expectedTestSessionState));
    }

    [Test]
    public async Task ResponseHttpContentTypeHeaderHasCorrectValue()
    {
        // Arrange
        const string expectedContentType = "text/html";
        const string expectedCharSet = "utf-8";

        // Act
        using TestSession session = await GetTestSessionFromInMemoryHttpTestServer(
                responseBuilder: SimplePageResponseBuilder);

        var contentType = session.GetNonSerializablePropertyBagValue<BrowserResponseMessage>(
            PropertyBagKeys.BrowserResponseMessage)!.HttpResponseMessage.Content.Headers.ContentType;

        // Assert
        Assert.That(contentType, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(contentType.MediaType, Is.EqualTo(expectedContentType));
            Assert.That(contentType.CharSet, Is.EqualTo(expectedCharSet));
        });
    }

    [Test]
    public async Task TestSessionFromBrowserTestAgentContainsAllTestStepsWhenSucceeded()
    {
        // Arrange
        const int expectedTestStepsCount = 3;

        // Act
        using TestSession session = await GetTestSessionFromInMemoryHttpTestServer(
                responseBuilder: SimplePageResponseBuilder)
            .ConfigureAwait(false);

        // Assert
        Assert.That(session.Steps, Has.Count.EqualTo(expectedTestStepsCount));
    }

    [Test]
    public async Task DnsLookupTestStepHasResolvedIPAddressesWhenSucceeded()
    {
        // Arrange 
        PropertyBagKey expectedBag = PropertyBagKeys.DnsResolvedIPAddresses;

        // Act
        await using TestSession session = await GetTestSessionFromInMemoryHttpTestServer(
                responseBuilder: SimplePageResponseBuilder)
            .ConfigureAwait(false);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(session.Steps.Any(step => step.Name == DnsLookup.ComponentName), Is.True);
            Assert.That(session.TryGetPropertyBagValue(expectedBag, out PropertyBagValue<string[]>? ips), Is.True);
        });
    }

    [Test]
    public async Task IPAddressAccessibilityCheckStepHasValidatedIPAddressWhenSucceeded()
    {
        // Arrange 
        PropertyBagKey expectedBag = PropertyBagKeys.IPAddress;

        // Act
        await using TestSession session = await GetTestSessionFromInMemoryHttpTestServer(
                responseBuilder: SimplePageResponseBuilder)
            .ConfigureAwait(false);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(session.Steps.Any(step => step.Name == IPAddressAccessibilityCheck.StepName), Is.True);
            Assert.That(session.TryGetPropertyBagValue(expectedBag, out PropertyBagValue<string>? ipaddress), Is.True);
        });
    }

    [Test]
    public async Task HeadlessBrowserRequestSenderSendsUserAgentHttpHeaderWhenConfigured()
    {
        // Arrange
        const string userAgent = "Chrome/51.0.2704.64 Safari/537.36";

        var configuration = new BrowserConfiguration();
        var httpRequestHeaders = new Dictionary<string, IEnumerable<string>>
        {
            { HeaderNames.UserAgent, [userAgent] }
        };
        configuration.PropertyBag.AddOrUpdateProperty(PropertyBagKeys.HttpRequestHeaders, httpRequestHeaders);

        bool requestReceived = false;

        void ValidateRequest(HttpListenerRequest request)
        {
            requestReceived = true;
            // Assert
            Assert.That(request.UserAgent, Is.Not.Null);
            Assert.That(request.UserAgent, Is.EqualTo(userAgent));
        }

        // Act
        using TestSession session = await GetTestSessionFromInMemoryHttpTestServer(
                responseBuilder: SimplePageResponseBuilder,
                requestReceived: ValidateRequest, 
                configuration: configuration)
            .ConfigureAwait(false);

        Assert.That(requestReceived, Is.True);
    }

    [Test]
    public async Task HeadlessBrowserRequestSenderShouldSendCustomHttpHeaderWhenConfigured()
    {
        // Arrange
        const string customKey = "custom-key";
        const string customValue = "This is custome header";

        var configuration = new BrowserConfiguration();
        var httpRequestHeaders = new Dictionary<string, IEnumerable<string>>
        {
            { customKey, [customValue] }
        };
        configuration.SetHttpRequestHeaders(httpRequestHeaders);

        bool requestReceived = false;

        void ValidateRequest(HttpListenerRequest request)
        {
            requestReceived = true;
            // Assert
            Assert.That(request.Headers.AllKeys.Any(k => k == customKey), Is.True);
            Assert.That(() =>
            {
                for (int i = 0; i < request.Headers.Count; i++)
                {
                    var hasValue = request.Headers.GetValues(i)?.Any(v => 
                        v.Contains(customValue, StringComparison.InvariantCulture));

                    if (hasValue == true)
                    {
                        return true;
                    }
                }

                return false; // If we are here, then we did not find expected value;
            });
        }

        // Act
        using TestSession session = await GetTestSessionFromInMemoryHttpTestServer(
                responseBuilder: SimplePageResponseBuilder,
                requestReceived: ValidateRequest,
                configuration: configuration)
            .ConfigureAwait(false);

        Assert.That(requestReceived, Is.True);
    }

    [TestCase("GET")]
    [TestCase("POST")]
    [TestCase("DELETE")]
    [Test]
    public async Task HeadlessBrowserRequestComponentUsesConfiguredHttpMethod(string method)
    {
        // Arrange
        var httpMethod = new HttpMethod(method);

        var configuration = new BrowserConfiguration();
        configuration.SetHttpMethod(httpMethod);

        bool requestReceived = false;

        void ValidateRequest(HttpListenerRequest request)
        {
            requestReceived = true;
            // Assert
            Assert.That(request.HttpMethod, Is.EqualTo(method));
        }

        // Act
        using TestSession session = await GetTestSessionFromInMemoryHttpTestServer(
                responseBuilder: SimplePageResponseBuilder,
                requestReceived: ValidateRequest,
                configuration: configuration)
            .ConfigureAwait(false);

        Assert.That(requestReceived, Is.True);
    }

    [Test]
    public async Task HeadlessBrowserRequestComponentUsesConfiguredHttpContent()
    {
        // Arrange
        var configuration = new BrowserConfiguration();
        using var stringContent = new StringContent("HttpContentData");
        configuration.SetHttpContent(stringContent);

        bool requestReceived = false;

        void ValidateRequest(HttpListenerRequest request)
        {
            requestReceived = true;
            Assert.Multiple(() =>
            {
                // Assert
                Assert.That(request.HasEntityBody, Is.True);
                Assert.That(request.ContentType, Is.EqualTo("text/plain"));
            });
        }

        // Act
        using TestSession session = await GetTestSessionFromInMemoryHttpTestServer(
                responseBuilder: SimplePageResponseBuilder,
                requestReceived: ValidateRequest,
                configuration: configuration)
            .ConfigureAwait(false);

        Assert.That(requestReceived, Is.True);
    }

    [Test]
    public async Task HeadlessBrowserRequestComponentUsesConfiguredJsonContent()
    {
        // Arrange
        var configuration = new BrowserConfiguration();
        using var jsonContent = JsonContent.Create("{\"name\":\"John\", \"age\":30, \"car\":null}");
        configuration.SetHttpContent(jsonContent);

        bool requestReceived = false;

        void ValidateRequest(HttpListenerRequest request)
        {
            requestReceived = true;
            Assert.Multiple(() =>
            {
                // Assert
                Assert.That(request.HasEntityBody, Is.True);
                Assert.That(request.ContentType, Is.EqualTo("application/json"));
            });
        }

        // Act
        using TestSession session = await GetTestSessionFromInMemoryHttpTestServer(
                responseBuilder: SimplePageResponseBuilder,
                requestReceived: ValidateRequest,
                configuration: configuration)
            .ConfigureAwait(false);

        Assert.That(requestReceived, Is.True);
    }

    [Test]
    public async Task HeadlessBrowserRequestComponentDoesNotUseContentTypeWhenNotConfigured()
    {
        // Arrange
        var configuration = new BrowserConfiguration();
        using var stringContent = new StringContent("HttpContentData");
        configuration.SetHttpContent(stringContent, setContentHeaders: false);

        bool requestReceived = false;

        void ValidateRequest(HttpListenerRequest request)
        {
            requestReceived = true;
            Assert.Multiple(() =>
            {
                // Assert
                Assert.That(request.HasEntityBody, Is.True);
                Assert.That(request.ContentType, Is.Null);
            });
        }

        // Act
        using TestSession session = await GetTestSessionFromInMemoryHttpTestServer(
                responseBuilder: SimplePageResponseBuilder,
                requestReceived: ValidateRequest,
                configuration: configuration)
            .ConfigureAwait(false);

        Assert.That(requestReceived, Is.True);
    }

    [Test]
    public async Task HeadlessBrowserRequestComponentDoesNotUsesHttpContentWhenNotConfigured()
    {
        // Arrange
        var configuration = new BrowserConfiguration();

        bool requestReceived = false;

        void ValidateRequest(HttpListenerRequest request)
        {
            requestReceived = true;
            // Assert
            Assert.That(request.HasEntityBody, Is.False);
        }

        // Act
        using TestSession session = await GetTestSessionFromInMemoryHttpTestServer(
                responseBuilder: SimplePageResponseBuilder,
                requestReceived: ValidateRequest,
                configuration: configuration)
            .ConfigureAwait(false);

        Assert.That(requestReceived, Is.True);
    }

    [Test]
    [Ignore("I need to come up with a better design for this unit tests. There are very flaky and unreliable.")]
    public async Task HeadlessBrowserRequestSenderHasExpectedErrorMessageWhenTimeouts()
    {
        // Arrange
        const string expectedErrMsg =
            "Error 1000: Message: Timeout 1000ms exceeded.\nCall log:\n  - navigating to \"http://localhost:8080/\", " +
            "waiting until \"load\"";

        var configuration = new BrowserConfiguration
        {
            HttpRequestTimeout = TimeSpan.FromSeconds(1)
        };

        void ResponseBuilder(HttpListenerResponse response)
        {
            TimeSpan timeout = configuration.HttpRequestTimeout;

            // Delay response time which is > than TimeOut value defined in TestSettings
            // so we can test if the BrowserTestAgent correctly reacts to timeout configuration.
            Thread.Sleep(timeout + TimeSpan.FromSeconds(1));
            var file = new FileInfo(@"HttpServer/Pages/SimplePage.html");
            using var fileStream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read);

            // Set the headers for the response
            response.StatusCode = (int)HttpStatusCode.OK;
            response.ContentType = "text/html; charset=utf-8";
            response.ContentLength64 = fileStream.Length;

            // Write the file contents to the response stream
            fileStream.CopyTo(response.OutputStream);
            fileStream.Close();
            response.Close();
        }

        // Act
        using TestSession session = await GetTestSessionFromInMemoryHttpTestServer(
            ResponseBuilder, configuration: configuration).ConfigureAwait(false);

        var failedStep = session.Steps.FirstOrDefault(step =>
            step.Name.StartsWith(nameof(BrowserRequestSender), StringComparison.InvariantCulture) &&
            step.Result == TestStepResult.Failed);

        // Assert
        Assert.That(failedStep, Is.Not.Null);
        Assert.That(failedStep.ErrorMessage, Is.EqualTo(expectedErrMsg));
    }

    [Test]
    public async Task HeadlessBrowserRequestSenderStepAddsHttpResponseContentToPropertyBag()
    {
        // Act
        using TestSession session = await GetTestSessionFromInMemoryHttpTestServer(
                responseBuilder: SimplePageResponseBuilder)
            .ConfigureAwait(false);

        // Assert
        PropertyBagValue<byte[]>? byteArray = null;

        Assert.Multiple(() =>
        {
            Assert.That(session.TryGetPropertyBagValue(PropertyBagKeys.HttpContent, out byteArray), Is.True);
            Assert.That(byteArray is not null);
        });
    }

    [Test]
    public async Task HeadlessBrowserExecutesJavascriptWhenIncludedInWebPage()
    {
        // Arrange
        string expectedText = $"{DateTime.Now.Year}";

        // Act
        await using TestSession session = await GetTestSessionFromInMemoryHttpTestServer(
                responseBuilder: JavascriptPageResponseBuilder)
            .ConfigureAwait(false);

        var byteArray = session.GetPropertyBagValue<byte[]>(PropertyBagKeys.HttpContent);
        var content = Encoding.UTF8.GetString(byteArray!);

        // Assert
        Assert.That(content.Contains(expectedText, StringComparison.InvariantCulture), Is.True);
    }

    [Test]
    [Ignore("[Needs investigation] This test hangs when run with others but passes when executed alone.")]
    public async Task HeadlessBrowserRequestSenderComponentFollowsRedirectWhenConfigured()
    {
        // Arrange
        string destinationServerAddress = "http://localhost:8081/";
        HttpStatusCode redirectionStatus = HttpStatusCode.MovedPermanently;

        using Task destinationServer = InMemoryHttpServer.TestServer(
            responseBuilder: (response) =>
            {
                response.StatusCode = (int)HttpStatusCode.OK;
                response.Close(); // By closing a response it can be send to client.
            },
            requestReceived: (request) => { },
            uriPrefixes: [new Uri(destinationServerAddress)]);

        var configuration = new BrowserConfiguration
        {
            FollowHttpRedirectionResponses = true
        };

        // Act
        var session = await GetTestSessionFromInMemoryHttpTestServer(
            responseBuilder: (response) =>
            {
                response.StatusCode = (int)redirectionStatus;
                response.Headers.Add(HeaderNames.Location, destinationServerAddress);
                response.Close(); // By closing a response it can be send to client.
            }, configuration: configuration);

        await destinationServer.WaitAsync(cancellationToken: default);

        await using (session.ConfigureAwait(false))
        {
            // Assert
            var httpRequestSteps = session.Steps.Where(s =>
                s.Name.StartsWith(nameof(BrowserRequestSender), StringComparison.InvariantCulture)).ToList();
            var redirectionStep = httpRequestSteps.First();
            var destinationStep = httpRequestSteps.Last();

            var redirectionStatusCode = redirectionStep.PropertyBag!.GetProperty<PropertyBagValue<string>>(
                PropertyBagKeys.HttpStatus).Value;
            var redirectionUrl = redirectionStep.PropertyBag!.GetProperty<PropertyBagValue<Dictionary<string, string>>>(
                PropertyBagKeys.HttpResponseHeaders).Value[HeaderNames.Location.ToUpperInvariant()];
            var destinationStatusCode = destinationStep.PropertyBag!.GetProperty<PropertyBagValue<string>>(
                PropertyBagKeys.HttpStatus).Value;

            Assert.Multiple(() =>
            {
                Assert.That(httpRequestSteps, Has.Count.EqualTo(2));
                Assert.That(redirectionStep, Is.Not.Null);
                Assert.That(redirectionStatusCode, Is.EqualTo($"{(int)redirectionStatus}"));
                Assert.That(redirectionUrl, Is.EqualTo(destinationServerAddress));
                Assert.That(destinationStep, Is.Not.Null);
                Assert.That(destinationStatusCode, Is.EqualTo($"{(int)HttpStatusCode.OK}"));
            });
        }
    }

    [Test]
    [Ignore("[Needs investigation] This test hangs when run with others but passes when executed alone.")]
    public async Task HeadlessBrowserRequestSenderComponentDetectsCircularDependency()
    {
        // Arrange
        // The redirect URL is on the same server as the original URL
        string destinationServerAddress = InMemoryHttpServer.GetTestServerAddress().AbsoluteUri;

        var configuration = new BrowserConfiguration
        {
            FollowHttpRedirectionResponses = true
        };

        // Act
        using TestSession session = await GetTestSessionFromInMemoryHttpTestServer(
            responseBuilder: (response) =>
            {
                response.StatusCode = (int)HttpStatusCode.MovedPermanently;
                response.Headers.Add(HeaderNames.Location, destinationServerAddress);
                response.Close(); // By closing a response it can be send to client.
            }, configuration: configuration);

        // Assert
        var httpRequestSteps = session.Steps.Where(s =>
            s.Name.StartsWith(nameof(BrowserRequestSender), StringComparison.InvariantCulture)).ToList();
        var redirectionStep = httpRequestSteps.First();
        var destinationStep = httpRequestSteps.Last();

        var redirectionStatusCode = redirectionStep.PropertyBag!.GetProperty<PropertyBagValue<string>>(
            PropertyBagKeys.HttpStatus).Value;
        var redirectionUrl = redirectionStep.PropertyBag!.GetProperty<PropertyBagValue<Dictionary<string, string>>>(
            PropertyBagKeys.HttpResponseHeaders).Value[HeaderNames.Location.ToUpperInvariant()];

        Assert.Multiple(() =>
        {
            Assert.That(httpRequestSteps, Has.Count.EqualTo(2));
            Assert.That(redirectionStep, Is.Not.Null);
            Assert.That(redirectionStep.Result, Is.EqualTo(TestStepResult.Succeeded));
            Assert.That(destinationStep.Result, Is.EqualTo(TestStepResult.Failed));
            Assert.That(destinationStep.ErrorMessage, Is.EqualTo("Error 1000: Message: A circular dependency was " +
                "detected for the URL http://localhost:8080/. The redirection chain is: " +
                "http://localhost:8080/ -> http://localhost:8080/."));
        });
    }

    [Test]
    [Ignore("[Needs investigation] This test hangs when run with others but passes when executed alone.")]
    public async Task HeadlessBrowserRequestSenderComponentReportsProgressForAllItsStepsWhenRedirectionHappens()
    {
        // Arrange
        var mockProgress = _serviceProvider.GetService<IProgress<TestStep>>();

        // The redirect URL is on the same server as the original URL
        string destinationServerAddress = InMemoryHttpServer.GetTestServerAddress().AbsoluteUri;
        var configuration = new BrowserConfiguration
        {
            FollowHttpRedirectionResponses = true
        };

        // Act
        using TestSession session = await GetTestSessionFromInMemoryHttpTestServer(
            responseBuilder: (response) =>
            {
                response.StatusCode = (int)HttpStatusCode.MovedPermanently;
                response.Headers.Add(HeaderNames.Location, destinationServerAddress);
                response.Close(); // By closing a response it can be send to client.
            }, configuration: configuration);

        // Assert
        Mock.Get(mockProgress!).Verify(p => p.Report(It.IsAny<TestStep>()), Times.Exactly(session.Steps.Count));
    }

    [Test]
    [Ignore("[Needs investigation] This test hangs when run with others but passes when executed alone.")]
    public async Task HeadlessBrowserRequestSenderComponentRestrictsNumberOfRedirectionsWhenConfigured()
    {
        // Arrange two HTTP redirections localhost:8080 -> localhost:8081 -> localhost:8082
        string destinationServerAddress = "http://localhost:8081/";
        HttpStatusCode redirectionStatus = HttpStatusCode.MovedPermanently;

        using Task destinationServer = InMemoryHttpServer.TestServer(
            responseBuilder: (response) =>
            {
                response.StatusCode = (int)HttpStatusCode.MovedPermanently;
                response.Headers.Add(HeaderNames.Location, "http://localhost:8082/");
                response.Close(); // By closing a response it can be send to client.
            },
            requestReceived: (request) => { },
            uriPrefixes: [new Uri(destinationServerAddress)]);

        var configuration = new BrowserConfiguration
        {
            FollowHttpRedirectionResponses = true,
            MaxRedirections = 1
        };

        // Act
        using TestSession session = await GetTestSessionFromInMemoryHttpTestServer(
            responseBuilder: (response) =>
            {
                response.StatusCode = (int)redirectionStatus;
                response.Headers.Add(HeaderNames.Location, destinationServerAddress);
                response.Close(); // By closing a response it can be send to client.
            }, configuration: configuration);

        await destinationServer.WaitAsync(cancellationToken: default);

        // Assert
        var httpRequestSteps = session.Steps.Where(s =>
            s.Name.StartsWith(nameof(BrowserRequestSender), StringComparison.InvariantCulture)).ToList();
        var destinationStep = httpRequestSteps.Last();

        Assert.Multiple(() =>
        {
            Assert.That(destinationStep.Result, Is.EqualTo(TestStepResult.Failed));
            Assert.That(destinationStep.ErrorMessage, Does.Contain($"The maximum number of redirects " +
                $"({configuration.MaxRedirections}) has been exceeded for the URL http://localhost:8080/. The last " +
                $"redirect URL was http://localhost:8081/"));
        });
    }

    [Test]
    [Ignore("[Needs investigation] This test hangs when run with others but passes when executed alone.")]
    public async Task HeadlessBrowserRequestSenderComponentFollowsRelativeRedirectedUrl()
    {
        bool redirected = false;

        // Arrange
        using Task redirectionServer = InMemoryHttpServer.TestServer(
            responseBuilder: (response) =>
            {
                if (!redirected)
                {
                    response.StatusCode = (int)HttpStatusCode.Moved;
                    response.Headers.Add(HeaderNames.Location, "/home");
                    response.Close(); // By closing a response it can be send to client.
                    redirected = true;
                }
                else
                {
                    response.StatusCode = (int)HttpStatusCode.OK;
                    response.Close(); // By closing a response it can be send to client.
                }
            },
            requestReceived: (request) => { },
            uriPrefixes: [new Uri("http://localhost:8081/"), new Uri("http://localhost:8081/home/")]);

        // Act
        using TestSession session = await GetTestSessionFromInMemoryHttpTestServer(
            responseBuilder: (response) =>
            {
                response.StatusCode = (int)HttpStatusCode.Moved;
                response.Headers.Add(HeaderNames.Location, "http://localhost:8081/");
                response.Close(); // By closing a response it can be send to client.
            }).ConfigureAwait(false);

        await redirectionServer.WaitAsync(cancellationToken: default);

        // Assert
        var httpRequestSteps = session.Steps.Where(s =>
            s.Name.StartsWith(nameof(BrowserRequestSender), StringComparison.InvariantCulture)).ToList();
        var destinationStep = httpRequestSteps.Last();

        var destinationStatusCode = destinationStep.PropertyBag!.GetProperty<PropertyBagValue<string>>(
            PropertyBagKeys.HttpStatus).Value;
        var destinationUrl = destinationStep.PropertyBag!.GetProperty<NonSerializable<BrowserResponseMessage>>(
            PropertyBagKeys.BrowserResponseMessage).Value;

        Assert.Multiple(() =>
        {
            Assert.That(httpRequestSteps, Has.Count.EqualTo(3));
            Assert.That(destinationStep, Is.Not.Null);
            Assert.That(destinationStatusCode, Is.EqualTo($"{(int)HttpStatusCode.OK}"));
            Assert.That(destinationUrl, Is.Not.Null);
            Assert.That(destinationUrl.HttpResponseMessage.RequestMessage!.RequestUri!.AbsoluteUri,
                Is.EqualTo($"http://localhost:8081/home"));
        });
    }

    [Test]
    [Ignore(reason: "https://github.com/XPing365/xping365-sdk/issues/35")]
    public async Task HeadlessBrowserRequestSenderComponentDoesNotFollowHttpRedirectWhenFollowRedirectionIsDisabled()
    {
        // Arrange
        string destinationServerAddress = "http://localhost:8080/destination";
        var configuration = new BrowserConfiguration
        {
            FollowHttpRedirectionResponses = false
        };

        // Act
        using TestSession session = await GetTestSessionFromInMemoryHttpTestServer(
            responseBuilder: (response) =>
            {
                response.StatusCode = (int)HttpStatusCode.MovedPermanently;
                response.Headers.Add(HeaderNames.Location, destinationServerAddress);
                response.Close(); // By closing a response it can be send to client.
            }, configuration: configuration).ConfigureAwait(false);

        // Assert
        var httpRequestSteps = session.Steps.Where(s =>
            s.Name.StartsWith(nameof(BrowserRequestSender), StringComparison.InvariantCulture)).ToList();

        Assert.That(httpRequestSteps, Has.Count.EqualTo(1));
    }

    private async Task<TestSession> GetTestSessionFromInMemoryHttpTestServer(
        Action<HttpListenerResponse> responseBuilder,
        Action<HttpListenerRequest>? requestReceived = null,
        TestComponent? component = null,
        TestSettings? settings = null,
        BrowserConfiguration? configuration = null,
        CancellationToken cancellationToken = default)
    {
        static void RequestReceived(HttpListenerRequest request) { };

        using Task testServer = InMemoryHttpServer.TestServer(
            responseBuilder, 
            requestReceived ?? RequestReceived, 
            cancellationToken);

        var testAgent = _serviceProvider.GetRequiredKeyedService<TestAgent>(serviceKey: "BrowserClient");
        testAgent.Replace(new BrowserRequestSender(configuration ?? new BrowserConfiguration()));

        if (component != null)
        {
            testAgent.Container.AddComponent(component);
        }

        TestSession session = await testAgent
            .RunAsync(
                url: InMemoryHttpServer.GetTestServerAddress(),
                settings: settings ?? new TestSettings() { Timeout = TimeSpan.FromHours(1) },
                cancellationToken: cancellationToken);

        await testServer.WaitAsync(cancellationToken);

        Debug.WriteLine("InMemory TestServer completed.");

        return session;
    }

    private static void SimplePageResponseBuilder(HttpListenerResponse response) =>
        PageResponseBuilder(response, new FileInfo(@"HttpServer/Pages/SimplePage.html"));

    private static void JavascriptPageResponseBuilder(HttpListenerResponse response) =>
        PageResponseBuilder(response, new FileInfo(@"HttpServer/Pages/JavascriptPage.html"));

    private static void PageResponseBuilder(HttpListenerResponse response, FileInfo fileToRespond)
    {
        using var fileStream = new FileStream(fileToRespond.FullName, FileMode.Open, FileAccess.Read);

        // Set the headers for the response
        response.StatusCode = (int)HttpStatusCode.OK;
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = fileStream.Length;

        // Write the file contents to the response stream
        fileStream.CopyTo(response.OutputStream);
        fileStream.Close();
        response.Close();
    }
}
