using System.Net;
using Xping.Sdk.Validations.Content.Html;
using Xping.Sdk.Validations.TextUtils;

namespace Xping.Sdk.Validations.HttpResponse;

/// <summary>
/// Represents an object for validating an HTTP response.
/// </summary>
public interface IHttpAssertions
{
    /// <summary>
    /// Validates that the HTTP response contains a specific header.
    /// </summary>
    /// <param name="name">The name of the HTTP header to validate.</param>
    /// <param name="options">Optional parameter for customizing the search text behavior.</param>
    /// <returns>An instance of <see cref="IHttpAssertions"/> to allow for further assertions.</returns>
    IHttpAssertions ToHaveHeader(string name, TextOptions? options = null);

    /// <summary>
    /// Validates that the HTTP response contains a specific header with value.
    /// </summary>
    /// <param name="name">The name of the HTTP header to validate.</param>
    /// <param name="value">The value of the HTTP header to validate.</param>
    /// <param name="options">Optional parameter for customizing the search text behavior.</param>
    /// <returns>An instance of <see cref="IHttpAssertions"/> to allow for further assertions.</returns>
    IHttpAssertions ToHaveHeaderWithValue(string name, string value, TextOptions? options = null);

    /// <summary>
    /// Validates the Content-Type header of the HTTP response.
    /// </summary>
    /// <param name="contentType">The content-type value to validate.</param>
    /// <param name="options">Optional parameter for customizing the search text behavior.</param>
    /// <returns>An instance of <see cref="IHttpAssertions"/> to allow for further assertions.</returns>
    IHttpAssertions ToHaveContentType(string contentType, TextOptions? options = null);

    /// <summary>
    /// Validates that the response body contains a specific string.
    /// </summary>
    /// <param name="expectedContent">The string that is expected to be found in the response body.</param>
    /// <param name="options">Optional parameter for customizing the search text behavior.</param>
    /// <returns>An instance of <see cref="IHttpAssertions"/> to allow for further assertions.</returns>
    IHttpAssertions ToHaveBodyContaining(string expectedContent, TextOptions? options = null);

    /// <summary>
    /// Validates that the response time is less than a specified duration.
    /// </summary>
    /// <param name="maxDuration">The maximum allowable duration for the response time.</param>
    /// <returns>An instance of <see cref="IHttpAssertions"/> to allow for further assertions.</returns>
    IHttpAssertions ToHaveResponseTimeLessThan(TimeSpan maxDuration);

    /// <summary>
    /// Validates that the HTTP response has the specified status code.
    /// </summary>
    /// <param name="statusCode">The expected status code.</param>
    /// <returns>An instance of <see cref="IHttpAssertions"/> to allow for further assertions.</returns>
    IHttpAssertions ToHaveStatusCode(HttpStatusCode statusCode);

    /// <summary>
    /// Ensures that the HTTP response has a successful (2xx) status code.
    /// </summary>
    /// <returns>An instance of <see cref="IHttpAssertions"/> to allow for further assertions.</returns>
    IHttpAssertions ToHaveSuccessStatusCode();
}
