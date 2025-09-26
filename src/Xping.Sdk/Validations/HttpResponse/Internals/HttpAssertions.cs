/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Net;
using System.Net.Http.Headers;
using Microsoft.Net.Http.Headers;
using Xping.Sdk.Core.Common;
using Xping.Sdk.Core.Components;
using Xping.Sdk.Validations.TextUtils;
using Xping.Sdk.Validations.TextUtils.Internals;

namespace Xping.Sdk.Validations.HttpResponse.Internals;

internal class HttpAssertions(IHttpResponse httpResponse) : IHttpAssertions
{
    private readonly HttpResponseMessage _response = httpResponse.Response;
    private readonly TimeSpan _responseTime = httpResponse.ResponseTime;
    private readonly TestContext _context = httpResponse.Context;

    public IHttpAssertions ToHaveSuccessStatusCode()
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(HttpAssertions)}.{nameof(ToHaveSuccessStatusCode)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("To have success status code"));

        if (!_response.IsSuccessStatusCode)
        {
            throw new ValidationException("" +
                $"Expected success status code, but the actual status code was \"{(int)_response.StatusCode}\". This " +
                $"exception occurred as part of validating HTTP response data.");
        }

        // Create a successful test step with detailed information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return this;
    }

    public IHttpAssertions ToHaveStatusCode(HttpStatusCode statusCode)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(HttpAssertions)}.{nameof(ToHaveStatusCode)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("To have status code: " + (int)statusCode));

        if (statusCode != _response.StatusCode)
        {
            throw new ValidationException("" +
                $"Expected \"{(int)statusCode}\" status code, but the actual status code was " +
                $"\"{(int)_response.StatusCode}\". This exception occurred as part of validating HTTP response data.");
        }

        // Create a successful test step with detailed information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return this;
    }

    public IHttpAssertions ToHaveHeader(string name, TextOptions? options = null)
    {
        // Normalize header name and value
        var normalizedName = name.ToUpperInvariant().Trim();

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(HttpAssertions)}.{nameof(ToHaveHeader)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("To have HTTP header: " + name))
            .Build(
                new PropertyBagKey(key: nameof(TextOptions)),
                new PropertyBagValue<string>(options?.ToString() ?? "Null"));

        var normalizedHeaders = ConcatenateDictionaries(
            GetNormalizedHeaders(_response.Headers),
            GetNormalizedHeaders(_response.Content.Headers),
            GetNormalizedHeaders(_response.TrailingHeaders));

        if (!TryGetHeader(normalizedHeaders, normalizedName, out var header, options) || header == null)
            throw new ValidationException(
                $"Expected to find HTTP header \"{normalizedName}\", but no such header exists.");

        // Create a successful test step with detailed information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return this;
    }

    public IHttpAssertions ToHaveHeaderWithValue(string name, string value, TextOptions? options = null)
    {
        // Normalize header name and value
        var normalizedName = name.ToUpperInvariant().Trim();
        var normalizedValue = value.Trim();
        var textComparer = new TextComparer(options);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(HttpAssertions)}.{nameof(ToHaveHeaderWithValue)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"To have HTTP header: {normalizedName} with value: {normalizedValue}"))
            .Build(
                new PropertyBagKey(key: nameof(TextOptions)),
                new PropertyBagValue<string>(options?.ToString() ?? "Null"));

        var normalizedHeaders = ConcatenateDictionaries(
            GetNormalizedHeaders(_response.Headers),
            GetNormalizedHeaders(_response.Content.Headers),
            GetNormalizedHeaders(_response.TrailingHeaders));

        if (!TryGetHeaderValues(normalizedHeaders, normalizedName, out var headerValues, options) ||
            headerValues == null)
            throw new ValidationException(
                $"Expected to find HTTP header: \"{normalizedName}\", but no such header exists.");

        var enumerable = headerValues.ToList();
        if (!enumerable.Any(v => textComparer.Compare(v, normalizedValue)))
        {
            throw new ValidationException(
                $"Expected to find HTTP header: \"{normalizedName}\" with value: \"{normalizedValue}\", but the " +
                $"actual value was: \"{string.Join(";", enumerable)}\".");
        }

        // Create a successful test step with detailed information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return this;
    }

    public IHttpAssertions ToHaveContentType(string contentType, TextOptions? options = null)
    {
        return ToHaveHeaderWithValue(HeaderNames.ContentType, contentType, options);
    }

    public IHttpAssertions ToHaveBodyContaining(string expectedContent, TextOptions? options = null)
    {
        var normalizedExpectedContent = expectedContent.Trim();
        var textComparer = new TextComparer(options);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(HttpAssertions)}.{nameof(ToHaveBodyContaining)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"To have body containing: {normalizedExpectedContent}"))
            .Build(
                new PropertyBagKey(key: nameof(TextOptions)),
                new PropertyBagValue<string>(options?.ToString() ?? "Null"));

        var contentStream = _response.Content.ReadAsStream();
        using StreamReader reader = new(contentStream);
        var content = reader.ReadToEnd();

        if (!textComparer.Compare(content, normalizedExpectedContent))
            throw new ValidationException(
                $"Expected HTTP response body to contain \"{normalizedExpectedContent}\", but no such string was " +
                $"found.");

        // Create a successful test step with detailed information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return this;
    }

    public IHttpAssertions ToHaveResponseTimeLessThan(TimeSpan maxDuration)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(HttpAssertions)}.{nameof(ToHaveResponseTimeLessThan)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"To have response time less than: {maxDuration}"));

        if (_responseTime < maxDuration)
        {
            // Create a successful test step with detailed information about the current test operation.
            var testStep = _context.SessionBuilder.Build();
            // Report the progress of this test step.
            _context.Progress?.Report(testStep);

            return this;
        }

        throw new ValidationException(
            $"Expected HTTP response time to be less than {maxDuration}, but actual response " +
            $"time was {_responseTime}."
        );
    }

    private static Dictionary<string, IEnumerable<string>> GetNormalizedHeaders(HttpHeaders headers) =>
        headers.ToDictionary(h => h.Key.ToUpperInvariant(), h => h.Value);

    private static bool TryGetHeader(
        Dictionary<string, IEnumerable<string>> headers,
        string name,
        out string? value,
        TextOptions? options = null)
    {
        value = null;
        var textComparer = new TextComparer(options);

        foreach (var header in headers.Where(header => textComparer.Compare(header.Key, name)))
        {
            value = header.Key;
            return true;
        }

        return false;
    }

    private static bool TryGetHeaderValues(
        Dictionary<string, IEnumerable<string>> headers,
        string name,
        out IEnumerable<string>? values,
        TextOptions? options = null)
    {
        values = null;
        var textComparer = new TextComparer(options);

        foreach (var header in headers.Where(header => textComparer.Compare(header.Key, name)))
        {
            values = header.Value;
            return true;
        }

        return false;
    }

    private static Dictionary<string, IEnumerable<string>> ConcatenateDictionaries(
        params Dictionary<string, IEnumerable<string>>[] dictionaries)
    {
        var result = new Dictionary<string, IEnumerable<string>>();

        foreach (var dict in dictionaries)
        {
            foreach (var kvp in dict)
            {
                if (result.TryGetValue(kvp.Key, out var value))
                {
                    // Merge the values if the key already exists
                    result[kvp.Key] = value.Concat(kvp.Value);
                }
                else
                {
                    // Add the key-value pair if it doesn't exist
                    result.Add(kvp.Key, kvp.Value);
                }
            }
        }

        return result;
    }
}
