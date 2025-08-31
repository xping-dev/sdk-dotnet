/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using Xping.Sdk.Core.Components;

namespace Xping.Sdk.Validations.SslCertificate;

/// <summary>
/// Represents an SSL certificate response interface.
/// </summary>
public interface ISslCertificateResponse
{
    /// <summary>
    /// Gets the SSL certificate.
    /// </summary>
    internal X509Certificate2 Certificate { get; }

    /// <summary>
    /// Gets the certificate chain.
    /// </summary>
    internal X509Chain Chain { get; }

    /// <summary>
    /// Gets the SSL policy errors.
    /// </summary>
    internal SslPolicyErrors SslErrors { get; }

    /// <summary>
    /// Gets the test context associated with the SSL certificate validation.
    /// </summary>
    internal TestContext Context { get; }
}
