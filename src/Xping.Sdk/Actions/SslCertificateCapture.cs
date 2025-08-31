/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.DependencyInjection;
using Xping.Sdk.Core.Common;
using Xping.Sdk.Core.Components;
using Xping.Sdk.Core.Services;
using Xping.Sdk.Core.Session;

namespace Xping.Sdk.Actions;

/// <summary>
/// Captures SSL certificate information during HTTP requests and stores it in the test context.
/// This component configures the SSL capture service to intercept SSL certificate validation
/// and capture certificate details for later analysis.
/// </summary>
public sealed class SslCertificateCapture : TestComponent
{
    /// <summary>
    /// Key used to store the SSL certificate in the test context property bag.
    /// </summary>
    public const string CertificateDataKey = "SslCertificate";

    /// <summary>
    /// Key used to store SSL policy errors in the test context property bag.
    /// </summary>
    public const string SslErrorsKey = "SslPolicyErrors";

    /// <summary>
    /// Key used to store the certificate chain in the test context property bag.
    /// </summary>
    public const string CertificateChainKey = "SslCertificateChain";

    /// <summary>
    /// Key used to store the subject of the SSL certificate in the test context property bag.
    /// </summary>
    public const string CertificateSubjectKey = "SslCertificateSubject";

    /// <summary>
    /// Key used to store the issuer of the SSL certificate in the test context property bag.
    /// </summary>
    public const string CertificateIssuerKey = "SslCertificateIssuer";

    /// <summary>
    /// Key used to store the thumbprint of the SSL certificate in the test context property bag.
    /// </summary>
    public const string CertificateThumbprintKey = "SslCertificateThumbprint";

    /// <summary>
    /// Key used to store the serial number of the SSL certificate in the test context property bag.
    /// </summary>
    public const string CertificateSerialNumberKey = "SslCertificateSerialNumber";

    /// <summary>
    /// Key used to store the length of the SSL certificate chain in the test context property bag.
    /// </summary>
    public const string CertificateChainLengthKey = "SslCertificateChainLength";

    /// <summary>
    /// Key used to store the issuer name of the SSL certificate in the test context property bag.
    /// </summary>
    public const string CertificateIssuerNameKey = "SslCertificateIssuerName";

    /// <summary>
    /// Initializes a new instance of the SslCertificateCapture class.
    /// </summary>
    public SslCertificateCapture() :
        base(
            type: TestStepType.ActionStep,
            description: "Capture SSL certificate information",
            displayName: "SSL Certificate Capture")
    {
    }

    /// <summary>
    /// Configures SSL certificate capture for HTTP requests.
    /// This method configures the SSL capture service to capture certificate data during HTTP requests.
    /// </summary>
    /// <param name="url">A Uri object that represents the URL of the page being validated.</param>
    /// <param name="settings">A <see cref="TestSettings"/> object that contains the settings for the test.</param>
    /// <param name="context">A <see cref="TestContext"/> object that represents the test context.</param>
    /// <param name="serviceProvider">An instance object of a mechanism for retrieving a service object.</param>
    /// <param name="cancellationToken">An optional CancellationToken object that can be used to cancel this operation.</param>
    public override async Task HandleAsync(
        Uri url,
        TestSettings settings,
        TestContext context,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url, nameof(url));
        ArgumentNullException.ThrowIfNull(settings, nameof(settings));
        ArgumentNullException.ThrowIfNull(context, nameof(context));
        ArgumentNullException.ThrowIfNull(serviceProvider, nameof(serviceProvider));

        // Get the SSL capture configuration service
        var sslCaptureService = serviceProvider.GetRequiredService<ISslCaptureConfigurationService>();

        // Create property bag keys for storing certificate data
        var certKey = new PropertyBagKey(CertificateDataKey);
        var errorsKey = new PropertyBagKey(SslErrorsKey);
        var chainKey = new PropertyBagKey(CertificateChainKey);

        // Create a certificate capture callback that stores data in the test context
#pragma warning disable CA5359 // The purpose of this component is to capture certificate data, not validate it
        var callback = new RemoteCertificateValidationCallback(
            (sender, cert, chain, errors) =>
            {
                // Capture certificate information and store in property bag
                if (cert is X509Certificate2 certificate)
                {
                    // Create a copy of the certificate to avoid disposal issues
                    var certificateCopy = new X509Certificate2(certificate.RawData);
                    context.SessionBuilder.Build(certKey, new NonSerializable<X509Certificate2>(certificateCopy));
                    context.SessionBuilder.Build(errorsKey, new PropertyBagValue<string>(errors.ToString()));

                    if (certificate.Subject != null)
                    {
                        context.SessionBuilder.Build(
                            new PropertyBagKey(CertificateSubjectKey),
                            new PropertyBagValue<string>(certificate.Subject));
                    }

                    if (certificate.Issuer != null)
                    {
                        context.SessionBuilder.Build(
                            new PropertyBagKey(CertificateIssuerKey),
                            new PropertyBagValue<string>(certificate.Issuer));
                    }

                    if (certificate.IssuerName != null)
                    {
                        context.SessionBuilder.Build(
                            new PropertyBagKey(CertificateIssuerNameKey),
                            new PropertyBagValue<string>(certificate.IssuerName.Name));
                    }

                    if (certificate.Thumbprint != null)
                        {
                            context.SessionBuilder.Build(
                                new PropertyBagKey(CertificateThumbprintKey),
                                new PropertyBagValue<string>(certificate.Thumbprint));
                        }

                    if (certificate.SerialNumber != null)
                    {
                        context.SessionBuilder.Build(
                            new PropertyBagKey(CertificateSerialNumberKey),
                            new PropertyBagValue<string>(certificate.SerialNumber));
                    }

                    if (chain != null)
                    {
                        // Store chain information as a copy to avoid disposal issues
                        context.SessionBuilder.Build(chainKey, new NonSerializable<X509Chain>(chain));
                        context.SessionBuilder.Build(
                            new PropertyBagKey(CertificateChainLengthKey),
                            new PropertyBagValue<int>(chain.ChainElements.Count));
                    }
                }

                // Always return true to capture certificate data even for invalid certificates
                // Actual validation should be performed separately using a dedicated validator component
                return true;
            });
#pragma warning restore CA5359

        // Configure the SSL capture service with the callback
        sslCaptureService.ConfigureCallback(callback);

        await Task.CompletedTask.ConfigureAwait(false);
    }
}
