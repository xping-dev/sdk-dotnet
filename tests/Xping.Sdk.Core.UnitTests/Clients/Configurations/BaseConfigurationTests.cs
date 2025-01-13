/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using Microsoft.Net.Http.Headers;
using Microsoft.Playwright;
using System.Net.Http.Json;
using System.Net;
using System.Text;
using Xping.Sdk.Core.Clients;
using Xping.Sdk.Core.Common;
using Cookie = System.Net.Cookie;

namespace Xping.Sdk.Core.UnitTests.HttpClients.Configurations;

public sealed class BaseConfigurationTests
{
    class BaseConfigurationUnderTest : BaseConfiguration
    { }

    [Test]
    public void PropertyBagIsNotNullWhenNewlyInstantiated()
    {
        // Arrange
        var baseConfiguration = new BaseConfigurationUnderTest();

        // Assert
        Assert.That(baseConfiguration.PropertyBag, Is.Not.Null);
    }

    [Test]
    public void GetHttpMethodReturnsHttpGetWhenNotSpecified()
    {
        // Arrange
        var baseConfiguration = new BaseConfigurationUnderTest();

        // Assert
        Assert.That(baseConfiguration.GetHttpMethod(), Is.EqualTo(HttpMethod.Get));
    }

    [Test]
    public void GetHttpMethodReturnsSpecifiedValueWhenSet()
    {
        // Arrange
        HttpMethod specifiedHttpMethod = HttpMethod.Post;
        var baseConfiguration = new BaseConfigurationUnderTest();

        // Act
        baseConfiguration.PropertyBag.AddOrUpdateProperty(PropertyBagKeys.HttpMethod, specifiedHttpMethod);

        // Assert
        Assert.That(baseConfiguration.GetHttpMethod(), Is.EqualTo(specifiedHttpMethod));
    }


    [Test]
    public void GetHttpMethodShouldReturnGetByDefault()
    {
        // Arrange
        var baseConfiguration = new BaseConfigurationUnderTest();

        // Act
        HttpMethod httpMethod = baseConfiguration.GetHttpMethod();

        // Assert
        Assert.That(httpMethod, Is.EqualTo(HttpMethod.Get));
    }

    [Test]
    public void SetHttpContentStoresHttpContentWhenConfiured()
    {
        // Arrange
        var baseConfiguration = new BaseConfigurationUnderTest();
        using HttpContent httpContent = new StringContent("http content");

        // Act
        baseConfiguration.SetHttpContent(httpContent);

        // Assert
        Assert.That(baseConfiguration.GetHttpContent(), Is.EqualTo(httpContent));
    }

    [Test]
    public void SetHttpContentShouldThowExceptionWhenNullProved()
    {
        // Arrange
        var baseConfiguration = new BaseConfigurationUnderTest();
        HttpContent httpContent = null!;

        // Assert
        Assert.Throws<ArgumentNullException>(() => baseConfiguration.SetHttpContent(httpContent));
    }

    [Test]
    public void SetHttpContentShouldSetContentTypeWhenStringContent()
    {
        // Arrange
        var baseConfiguration = new BaseConfigurationUnderTest();
        using HttpContent httpContent = new StringContent("http content");

        // Act
        baseConfiguration.SetHttpContent(httpContent);

        // Assert
        Assert.That(
            baseConfiguration.GetHttpRequestHeaders().TryGetValue(HeaderNames.ContentType, out var headers), Is.True);
        Assert.That(headers?.First(), Is.Not.Null);
        Assert.That(headers?.First(), Is.EqualTo("text/plain"));
    }

    [Test]
    public void SetHttpContentShouldSetContentTypeWhenJsonContent()
    {
        // Arrange
        var baseConfiguration = new BaseConfigurationUnderTest();
        using HttpContent httpContent = JsonContent.Create("{\"name\":\"John\", \"age\":30, \"car\":null}");

        // Act
        baseConfiguration.SetHttpContent(httpContent);

        // Assert
        Assert.That(
            baseConfiguration.GetHttpRequestHeaders().TryGetValue(HeaderNames.ContentType, out var headers), Is.True);
        Assert.That(headers?.First(), Is.Not.Null);
        Assert.That(headers?.First(), Is.EqualTo("application/json"));
    }

    [Test]
    public void SetHttpContentShouldSetContentTypeWhenFormUrlEncodedContent()
    {
        // Arrange
        var baseConfiguration = new BaseConfigurationUnderTest();
        using HttpContent httpContent = new FormUrlEncodedContent([new KeyValuePair<string, string>("", "")]);

        // Act
        baseConfiguration.SetHttpContent(httpContent);

        // Assert
        Assert.That(
            baseConfiguration.GetHttpRequestHeaders().TryGetValue(HeaderNames.ContentType, out var headers), Is.True);
        Assert.That(headers?.First(), Is.Not.Null);
        Assert.That(headers?.First(), Is.EqualTo("application/x-www-form-urlencoded"));
    }

    [Test]
    public void SetHttpContentShouldSetContentTypeWhenMultipartFormDataContent()
    {
        // Arrange
        var baseConfiguration = new BaseConfigurationUnderTest();
        using HttpContent httpContent = new MultipartFormDataContent();

        // Act
        baseConfiguration.SetHttpContent(httpContent);

        // Assert
        Assert.That(
            baseConfiguration.GetHttpRequestHeaders().TryGetValue(HeaderNames.ContentType, out var headers), Is.True);
        Assert.That(headers?.First(), Is.Not.Null);
        Assert.That(headers?.First(), Is.EqualTo("multipart/form-data"));
    }

    [Test]
    public void SetHttpContentShouldSetContentTypeWhenByteArrayContent()
    {
        // Arrange
        var baseConfiguration = new BaseConfigurationUnderTest();
        using HttpContent httpContent = new ByteArrayContent(Encoding.UTF8.GetBytes("Text"));

        // Act
        baseConfiguration.SetHttpContent(httpContent);

        // Assert
        Assert.That(
            baseConfiguration.GetHttpRequestHeaders().TryGetValue(HeaderNames.ContentType, out var headers), Is.True);
        Assert.That(headers?.First(), Is.Not.Null);
        Assert.That(headers?.First(), Is.EqualTo("application/octet-stream"));
    }

    [Test]
    public void SetHttpContentShouldSetContentTypeWhenStreamContent()
    {
        // Arrange
        var baseConfiguration = new BaseConfigurationUnderTest();
        using var memoryStream = new MemoryStream();
        using HttpContent httpContent = new StreamContent(memoryStream);

        // Act
        baseConfiguration.SetHttpContent(httpContent);

        // Assert
        Assert.That(
            baseConfiguration.GetHttpRequestHeaders().TryGetValue(HeaderNames.ContentType, out var headers), Is.True);
        Assert.That(headers?.First(), Is.Not.Null);
        Assert.That(headers?.First(), Is.EqualTo("application/octet-stream"));
    }

    [Test]
    public void SetHttpContentShouldOverrideContentTypeWhenContentTypeAlreadyPresented()
    {
        // Arrange
        var baseConfiguration = new BaseConfigurationUnderTest();
        using var memoryStream = new MemoryStream();
        using HttpContent httpContent = new StreamContent(memoryStream);
        baseConfiguration.SetHttpRequestHeaders(new Dictionary<string, IEnumerable<string>>
        {
            { HeaderNames.ContentType, ["CustomContentType"] }
        });

        // Act
        baseConfiguration.SetHttpContent(httpContent);

        // Assert
        Assert.That(
            baseConfiguration.GetHttpRequestHeaders().TryGetValue(HeaderNames.ContentType, out var headers), Is.True);
        Assert.That(headers?.First(), Is.Not.Null);
        Assert.That(headers.Count(), Is.EqualTo(1));
        Assert.That(headers?.First(), Is.EqualTo("application/octet-stream"));
    }

    [Test]
    public void SetHttpContentShouldNotOverrideContentTypeWhenContentTypeAlreadyPresentedAndSetHeadersDisabled()
    {
        // Arrange
        var baseConfiguration = new BaseConfigurationUnderTest();
        using var memoryStream = new MemoryStream();
        using HttpContent httpContent = new StreamContent(memoryStream);
        baseConfiguration.SetHttpRequestHeaders(new Dictionary<string, IEnumerable<string>>
        {
            { HeaderNames.ContentType, ["CustomContentType"] }
        });

        // Act
        baseConfiguration.SetHttpContent(httpContent, setContentHeaders: false);

        // Assert
        Assert.That(
            baseConfiguration.GetHttpRequestHeaders().TryGetValue(HeaderNames.ContentType, out var headers), Is.True);
        Assert.That(headers?.First(), Is.Not.Null);
        Assert.That(headers.Count(), Is.EqualTo(1));
        Assert.That(headers?.First(), Is.EqualTo("CustomContentType"));
    }

    class CustomContentType : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            return Task.CompletedTask;
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    [Test]
    public void SetHttpContentShouldNotSetContentTypeWhenCustomContentType()
    {
        // Arrange
        var baseConfiguration = new BaseConfigurationUnderTest();
        using HttpContent httpContent = new CustomContentType();

        // Act
        baseConfiguration.SetHttpContent(httpContent);

        // Assert
        Assert.That(
            baseConfiguration.GetHttpRequestHeaders().TryGetValue(HeaderNames.ContentType, out var headers), Is.False);
        Assert.That(headers, Is.Null);
    }

    [Test]
    public void GetHttpContentShouldReturnNullWhenNotSpecified()
    {
        // Arrange
        var baseConfiguration = new BaseConfigurationUnderTest();

        // Assert
        Assert.That(baseConfiguration.GetHttpContent(), Is.Null);
    }

    [Test]
    public void GetHttpContentReturnsSpecifiedValueWhenSet()
    {
        // Arrange
        var baseConfiguration = new BaseConfigurationUnderTest();
        using HttpContent httpContent = new StringContent("http content");

        // Act
        baseConfiguration.SetHttpContent(httpContent);

        // Assert
        Assert.That(baseConfiguration.GetHttpContent(), Is.EqualTo(httpContent));
    }

    [Test]
    public void ClearHttpContentRemovesHttpContentFromBaseConfigurationUnderTest()
    {
        // Arrange
        var baseConfiguration = new BaseConfigurationUnderTest();
        using HttpContent httpContent = new StringContent("http content");
        baseConfiguration.SetHttpContent(httpContent);

        // Act
        baseConfiguration.ClearHttpContent();

        // Assert
        Assert.That(baseConfiguration.GetHttpContent(), Is.Null);
    }

    [Test]
    public void ClearHttpContentDoesNothingWhenHttpContentNotSpecifiedPreviously()
    {
        // Arrange
        var baseConfiguration = new BaseConfigurationUnderTest();

        // Assert
        Assert.DoesNotThrow(() => baseConfiguration.ClearHttpContent());
    }

    [Test]
    public void SetHttpRequestHeadersShouldSetsHttpHeadersWhenSpecified()
    {
        // Arrange
        var baseConfiguration = new BaseConfigurationUnderTest();
        var httpRequestHeaders = new Dictionary<string, IEnumerable<string>>
        {
            { HeaderNames.UserAgent, ["Chrome/51.0.2704.64 Safari/537.36"] }
        };

        // Act
        baseConfiguration.SetHttpRequestHeaders(httpRequestHeaders);

        // Assert
        Assert.That(baseConfiguration.PropertyBag.ContainsKey(PropertyBagKeys.HttpRequestHeaders), Is.True);
    }

    [Test]
    public void ClearHttpRequestHeadersShouldClearsHttpHeaders()
    {
        // Arrange
        var httpRequestHeaders = new Dictionary<string, IEnumerable<string>>
        {
            { HeaderNames.UserAgent, ["Chrome/51.0.2704.64 Safari/537.36"] }
        };
        var baseConfiguration = new BaseConfigurationUnderTest();
        baseConfiguration.SetHttpRequestHeaders(httpRequestHeaders);

        // Act
        baseConfiguration.ClearHttpRequestHeaders();

        // Assert
        Assert.That(baseConfiguration.PropertyBag.ContainsKey(PropertyBagKeys.HttpRequestHeaders), Is.False);
    }

    [Test]
    public void ClearHttpRequestHeadersShouldDoNothingWhenHttpRequestHeadersWereNotSetPreviously()
    {
        // Arrange
        var baseConfiguration = new BaseConfigurationUnderTest();

        // Act
        baseConfiguration.ClearHttpRequestHeaders();

        // Assert
        Assert.That(baseConfiguration.PropertyBag.ContainsKey(PropertyBagKeys.HttpRequestHeaders), Is.False);
    }

    [Test]
    public void GetHttpRequestHeadersShouldReturnEmptyDictionaryWhenNotSpecified()
    {
        // Arrange
        var baseConfiguration = new BaseConfigurationUnderTest();

        // Assert
        Assert.That(baseConfiguration.GetHttpRequestHeaders(), Is.Empty);
    }

    [Test]
    public void GetHttpRequestHeadersShouldReturnSpecifiedDictionaryWhenSet()
    {
        // Arrange
        var baseConfiguration = new BaseConfigurationUnderTest();
        var httpRequestHeaders = new Dictionary<string, IEnumerable<string>>
        {
            { HeaderNames.UserAgent, ["Chrome/51.0.2704.64 Safari/537.36"] }
        };

        // Act
        baseConfiguration.PropertyBag.AddOrUpdateProperty(PropertyBagKeys.HttpRequestHeaders, httpRequestHeaders);

        // Assert
        Assert.That(baseConfiguration.GetHttpRequestHeaders(), Is.EqualTo(httpRequestHeaders));
    }

    [Test]
    public void SetHttpMethodShouldStoreHttpMethod()
    {
        // Arrange
        var baseConfiguration = new BaseConfigurationUnderTest();

        // Act
        baseConfiguration.SetHttpMethod(HttpMethod.Put);

        // Assert
        Assert.That(baseConfiguration.GetHttpMethod(), Is.EqualTo(HttpMethod.Put));
    }

    [Test]
    public void DefaultBaseConfigurationUnderTestAlwaysReturnNewInstanceWhenCalled()
    {
        // To not affect default instance parameters, this instance should always be recreated.
        var baseConfiguration1 = new BaseConfigurationUnderTest();
        var baseConfiguration2 = new BaseConfigurationUnderTest();

        Assert.That(baseConfiguration1, Is.Not.EqualTo(baseConfiguration2));
    }

    [Test]
    public void AddCookieStoresCookie()
    {
        // Arrange
        var baseConfiguration = new BaseConfigurationUnderTest();

        // Act
        baseConfiguration.AddCookie(new Cookie("cookiename", "value"));

        // Assert
        Assert.That(baseConfiguration.GetHttpRequestHeaders().TryGetValue(HeaderNames.Cookie, out var cookies), Is.True);
        Assert.That(cookies, Is.Not.Null);
        Assert.That(cookies.First(), Is.EqualTo("cookiename=value"));
    }

    [Test]
    public void AddMultipleCookiesStoresMultipleCookies()
    {
        // Arrange
        var baseConfiguration = new BaseConfigurationUnderTest();

        // Act
        baseConfiguration.AddCookie(new Cookie("cookiename1", "value1"));
        baseConfiguration.AddCookie(new Cookie("cookiename2", "value2"));
        baseConfiguration.AddCookie(new Cookie("cookiename3", "value3"));

        // Assert
        Assert.That(baseConfiguration.GetCookies(), Has.Count.EqualTo(3));
    }

    [Test]
    public void GetCookiesReturnStoredCookies()
    {
        // Arrange
        var baseConfiguration = new BaseConfigurationUnderTest();
        var cookie = new Cookie("cookiename", "value");

        // Act
        baseConfiguration.AddCookie(cookie);

        // Assert
        Assert.That(baseConfiguration.GetCookies(), Has.Count.EqualTo(1));
        Assert.That(baseConfiguration.GetCookies().First(), Is.EqualTo(cookie));
    }

    [Test]
    public void ClearCookiesClearsStoredCookies()
    {
        // Arrange
        var baseConfiguration = new BaseConfigurationUnderTest();
        var cookie = new Cookie("cookiename", "value");
        baseConfiguration.AddCookie(cookie);

        // Act
        baseConfiguration.ClearCookies();

        // Assert
        Assert.That(baseConfiguration.GetCookies(), Has.Count.EqualTo(0));
    }

    [Test]
    public void AddCookieCollectionStoresCookies()
    {
        // Arrange
        var baseConfiguration = new BaseConfigurationUnderTest();
        var cookies = new CookieCollection
        {
            new Cookie("cookiename1", "value1"),
            new Cookie("cookiename2", "value2")
        };

        // Act
        baseConfiguration.AddCookies(cookies);

        // Assert
        Assert.That(baseConfiguration.GetCookies(), Has.Count.EqualTo(2));
    }

    [Test]
    public void SetCredentialsShouldStoreCredentials()
    {
        // Arrange
        var baseConfiguration = new BaseConfigurationUnderTest();

        // Act
        baseConfiguration.SetCredentials(new NetworkCredential("userName", "password"));

        // Assert
        Assert.That(
            baseConfiguration.GetHttpRequestHeaders().TryGetValue(HeaderNames.Authorization, out var headers), Is.True);
        Assert.That(headers?.First(), Is.Not.Null);
        Assert.That(headers?.First(), Is.EqualTo("Basic dXNlck5hbWU6cGFzc3dvcmQ="));
    }

    [Test]
    public void ClearCredentialsShouldClearCredentials()
    {
        // Arrange
        var baseConfiguration = new BaseConfigurationUnderTest();
        baseConfiguration.SetCredentials(new NetworkCredential("userName", "password"));

        // Act
        baseConfiguration.ClearCredentials();

        // Assert
        Assert.That(
            baseConfiguration.GetHttpRequestHeaders().TryGetValue(HeaderNames.Authorization, out var headers), Is.False);
        Assert.That(headers?.First(), Is.Null);
    }
}
