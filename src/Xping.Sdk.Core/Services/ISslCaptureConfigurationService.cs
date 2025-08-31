/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Net.Security;

namespace Xping.Sdk.Core.Services;

/// <summary>
/// Service for configuring SSL certificate capture during HTTP requests.
/// This service allows dynamic configuration of SSL certificate validation callbacks
/// on a per-test scope basis.
/// </summary>
public interface ISslCaptureConfigurationService
{
    /// <summary>
    /// Gets the current SSL certificate validation callback.
    /// </summary>
    RemoteCertificateValidationCallback? SslCertificateValidationCallback { get; }
    
    /// <summary>
    /// Configures the SSL certificate validation callback for the current scope.
    /// </summary>
    /// <param name="callback">The callback to use for SSL certificate validation.</param>
    void ConfigureCallback(RemoteCertificateValidationCallback callback);
    
    /// <summary>
    /// Clears the current SSL certificate validation callback.
    /// </summary>
    void ClearCallback();
}
