/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Clients.Http;

/// <summary>
/// Represents the configuration settings specific to HttpClient.
/// </summary>
/// <remarks>
/// Currently, this class does not introduce additional properties or methods beyond those provided by the 
/// BaseConfiguration class. It serves as a placeholder for HttpClient-specific configurations and may be expanded in 
/// the future with more detailed settings. The use of this class is intended to make the intention of the configuration 
/// clear when it is specifically related to HttpClient usage.
/// </remarks>
public class HttpClientConfiguration : BaseConfiguration
{
    /// <summary>
    /// Gets or sets a boolean value which determines whether to retry HTTP requests when they fail. Default is true.
    /// </summary>
    public bool RetryHttpRequestWhenFailed { get; set; } = true;
}
