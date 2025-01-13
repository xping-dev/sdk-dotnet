/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using Xping.Sdk.Core.Components;

namespace Xping.Sdk.Validations.HttpResponse;

/// <summary>
/// Represents an HTTP response interface.
/// </summary>
public interface IHttpResponse
{
    /// <summary>
    /// Gets the HTTP response message.
    /// </summary>
    internal HttpResponseMessage Response { get; }

    /// <summary>
    /// Gets the HTTP response time.
    /// </summary>
    internal TimeSpan ResponseTime { get; }

    /// <summary>
    /// Gets the test context associated with the response.
    /// </summary>
    internal TestContext Context { get; }
}
