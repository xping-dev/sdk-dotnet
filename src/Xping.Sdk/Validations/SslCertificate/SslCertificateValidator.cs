/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using Xping.Sdk.Actions;
using Xping.Sdk.Core.Common;
using Xping.Sdk.Core.Components;
using Xping.Sdk.Core.Session;
using Xping.Sdk.Core.Extensions;
using Xping.Sdk.Shared;
using Xping.Sdk.Validations.SslCertificate.Internals;

namespace Xping.Sdk.Validations.SslCertificate;

/// <summary>
/// Represents a validator for SSL certificates. This class provides a mechanism to assert the validity of SSL 
/// certificates based on user-defined criteria.
/// </summary>
public class SslCertificateValidator : TestComponent
{
    // Private field to hold the validation logic.
    private readonly Action<ISslCertificateResponse> _validation;

    /// <summary>
    /// Initializes a new instance of the SslCertificateValidator class with a specified validation action.
    /// </summary>
    /// <param name="validation">
    /// An Action delegate that encapsulates the validation logic for the SSL certificate.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when the validation action is null.</exception>
    public SslCertificateValidator(Action<ISslCertificateResponse> validation) : base(
        type: TestStepType.ValidateStep,
        description: "Validate SSL certificate against custom criteria",
        displayName: "SSL Certificate Validator")
    {
        _validation = validation.RequireNotNull(nameof(validation));
    }

    /// <summary>
    /// This method performs the test step operation asynchronously.
    /// </summary>
    /// <param name="url">A Uri object that represents the URL of the page being validated.</param>
    /// <param name="settings">A <see cref="TestSettings"/> object that contains the settings for the test.</param>
    /// <param name="context">A <see cref="TestContext"/> object that represents the test context.</param>
    /// <param name="serviceProvider">An instance object of a mechanism for retrieving a service object.</param>
    /// <param name="cancellationToken">
    /// An optional CancellationToken object that can be used to cancel this operation.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// If any of the following parameters: url, settings or context is null.
    /// </exception>
    public override Task HandleAsync(
        Uri url,
        TestSettings settings,
        TestContext context,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url, nameof(url));
        ArgumentNullException.ThrowIfNull(settings, nameof(settings));
        ArgumentNullException.ThrowIfNull(context, nameof(context));

        using var instrumentation = new InstrumentationTimer();
        TestStep? testStep = null;

        try
        {
            var certificateKey = new PropertyBagKey(SslCertificateCapture.CertificateDataKey);
            var chainKey = new PropertyBagKey(SslCertificateCapture.CertificateChainKey);
            var errorsKey = new PropertyBagKey(SslCertificateCapture.SslErrorsKey);

            var certificate = context.GetNonSerializablePropertyBagValue<X509Certificate2>(certificateKey);
            var chain = context.GetNonSerializablePropertyBagValue<X509Chain>(chainKey);
            var sslErrorsString = context.GetPropertyBagValue<string>(errorsKey);

            if (certificate == null || chain == null || sslErrorsString == null)
            {
                testStep = context.SessionBuilder.Build(error: Errors.InsufficientData(component: this));
            }
            else
            {
                // Parse SSL errors from string
                if (!Enum.TryParse<SslPolicyErrors>(sslErrorsString, out var sslErrors))
                {
                    sslErrors = SslPolicyErrors.None;
                }

                // Perform SSL certificate validation.
                _validation.Invoke(new SslCertificateInfo(certificate, chain, sslErrors, context));
            }
        }
        catch (ValidationException ex)
        {
            testStep = context.SessionBuilder.Build(Errors.ValidationFailed(component: this, errorMessage: ex.Message));
        }
        catch (Exception exception)
        {
            testStep = context.SessionBuilder.Build(exception);
        }
        finally
        {
            if (testStep != null)
            {
                context.Progress?.Report(testStep);
            }
        }

        return Task.CompletedTask;
    }
}
