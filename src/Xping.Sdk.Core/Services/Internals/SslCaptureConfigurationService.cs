/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Net.Security;

namespace Xping.Sdk.Core.Services.Internals;

/// <summary>
/// Default implementation of ISslCaptureConfigurationService.
/// This service manages SSL certificate validation callbacks on a per-scope basis,
/// allowing test components to dynamically configure certificate capture.
/// </summary>
internal class SslCaptureConfigurationService : ISslCaptureConfigurationService
{
    /// <summary>
    /// Gets the current SSL certificate validation callback.
    /// </summary>
    public RemoteCertificateValidationCallback? SslCertificateValidationCallback { get; private set; }
    
    /// <summary>
    /// Configures the SSL certificate validation callback for the current scope.
    /// </summary>
    /// <param name="callback">The callback to use for SSL certificate validation.</param>
    public void ConfigureCallback(RemoteCertificateValidationCallback callback)
    {
        SslCertificateValidationCallback = callback ?? throw new ArgumentNullException(nameof(callback));
    }
    
    /// <summary>
    /// Clears the current SSL certificate validation callback.
    /// </summary>
    public void ClearCallback()
    {
        SslCertificateValidationCallback = null;
    }
}
