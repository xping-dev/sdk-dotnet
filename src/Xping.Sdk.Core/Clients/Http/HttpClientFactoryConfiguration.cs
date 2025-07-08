/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Net;

namespace Xping.Sdk.Core.Clients.Http;

/// <summary>
/// Represents configuration settings specific to HttpClient instances created by the IHttpClientFactory.
/// These settings apply to the factory, and all HttpClient instances created by the factory will be constructed with 
/// these settings. Normally, these properties have predefined defaults; however, they can also be changed according to 
/// your requirements.
/// </summary>
public class HttpClientFactoryConfiguration : BaseConfiguration
{
    #region Named HTTP Clients

    /// <summary>
    /// The name of the HttpClient that does not perform any retry policies.
    /// </summary>
    /// <remarks>
    /// This constant is used to create or resolve a named HttpClient in the DependencyInjection service collection.
    /// </remarks>
    public const string HttpClientWithNoRetryPolicy = nameof(HttpClientWithNoRetryPolicy);

    /// <summary>
    /// The name of the HttpClient that performs retry policies.
    /// </summary>
    /// <remarks>
    /// This constant is used to create or resolve a named HttpClient in the DependencyInjection service collection.
    /// </remarks>
    public const string HttpClientWithRetryPolicy = nameof(HttpClientWithRetryPolicy);
    
    /// <summary>
    /// The name of the HttpClient that performs retry policies for uploading test sessions to Xping.
    /// </summary>
    /// <remarks>
    /// This constant is used to create or resolve a named HttpClient in the DependencyInjection service collection.
    /// </remarks>
    public const string HttpClientUploadSession = nameof(HttpClientUploadSession);

    /// <summary>
    /// The name of the HttpClient that performs requests for location detection services.
    /// </summary>
    /// <remarks>
    /// This constant is used to create or resolve a named HttpClient in the DependencyInjection service collection.
    /// </remarks>
    public const string HttpClientLocationDetection = nameof(HttpClientLocationDetection);

    #endregion Named HTTP Clients

    /// <summary>
    /// This constant is set to false, indicating that the HttpClient will not follow redirection responses 
    /// automatically. This allows the Xping to handle redirections manually.
    /// </summary>
    public const bool HttpClientAutoRedirects = false;

    /// <summary>
    /// This constant is set to false, meaning that the HttpClient will not handle cookies automatically. This allows 
    /// the Xping to manage cookies in a custom manner.
    /// </summary>
    public const bool HttpClientHandleCookies = false;

    /// <summary>
    /// Gets or sets how long a connection can be in the pool to be considered reusable. Default is 1 minute.
    /// See the remarks on <see cref="SocketsHttpHandler.PooledConnectionLifetime"/>.
    /// </summary>
    public TimeSpan PooledConnectionLifetime { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets the sleep durations to wait for on each retry. Default is 1[s] on 1st retry, 5[s] on 2nd retry and
    /// 10[s] on 3rd retry. See the remarks on <see cref="Microsoft.Extensions.Http.PolicyHttpMessageHandler"/>.
    /// </summary>
    public IEnumerable<TimeSpan> SleepDurations { get; set; } =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10)
    ];

    /// <summary>
    /// Gets or sets the number of exceptions or handled results that are allowed before opening the circuit.
    /// See the remarks on <see cref="Microsoft.Extensions.Http.PolicyHttpMessageHandler"/>.
    /// </summary>
    public int HandledEventsAllowedBeforeBreaking { get; set; } = 3;

    /// <summary>
    /// Gets or sets the duration the circuit will stay open before resetting. Default is 30[s].
    /// See the remarks on <see cref="Microsoft.Extensions.Http.PolicyHttpMessageHandler"/>.
    /// </summary>
    public TimeSpan DurationOfBreak { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets a type of decompression method used by the _handler for automatic decompression of the HTTP content
    /// response. Default is All.
    /// </summary>
    public DecompressionMethods AutomaticDecompression { get; set; } = DecompressionMethods.All;
}