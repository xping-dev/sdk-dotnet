/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Globalization;
using Xping.Sdk.Core.Common;
using Xping.Sdk.Core.Components;
using Xping.Sdk.Validations.TextUtils;
using Xping.Sdk.Validations.TextUtils.Internals;

namespace Xping.Sdk.Validations.SslCertificate.Internals;

internal class SslCertificateAssertions(ISslCertificateResponse sslCertificateResponse) : ISslCertificateAssertions
{
    private readonly X509Certificate2 _certificate = sslCertificateResponse.Certificate;
    private readonly X509Chain _chain = sslCertificateResponse.Chain;
    private readonly SslPolicyErrors _sslErrors = sslCertificateResponse.SslErrors;
    private readonly TestContext _context = sslCertificateResponse.Context;

    public ISslCertificateAssertions ToHaveSubjectContaining(string expectedSubject, TextOptions? options = null)
    {
        var normalizedExpectedSubject = expectedSubject.Trim();
        var textComparer = new TextComparer(options);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToHaveSubjectContaining)))
            .Build(
                new PropertyBagKey(key: nameof(expectedSubject)),
                new PropertyBagValue<string>(expectedSubject))
            .Build(
                new PropertyBagKey(key: nameof(normalizedExpectedSubject)),
                new PropertyBagValue<string>(normalizedExpectedSubject))
            .Build(
                new PropertyBagKey(key: nameof(options)),
                new PropertyBagValue<string>(options?.ToString() ?? "Null"))
            .Build(
                new PropertyBagKey(key: "ActualSubject"),
                new PropertyBagValue<string>(_certificate.Subject));

        if (!textComparer.Compare(_certificate.Subject, normalizedExpectedSubject))
        {
            throw new ValidationException(
                $"Expected SSL certificate subject to contain \"{normalizedExpectedSubject}\", but the actual " +
                $"subject was \"{_certificate.Subject}\". This exception occurred as part of validating SSL " +
                $"certificate data.");
        }

        // Create a successful test step with detailed information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return this;
    }

    public ISslCertificateAssertions ToHaveSubjectEqualTo(string expectedSubject, TextOptions? options = null)
    {
        var normalizedExpectedSubject = expectedSubject.Trim();
        // For exact matching, create options with MatchWholeWord = true
        var exactMatchOptions = options != null 
            ? options with { MatchWholeWord = true } 
            : new TextOptions(MatchWholeWord: true);
        var textComparer = new TextComparer(exactMatchOptions);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToHaveSubjectEqualTo)))
            .Build(
                new PropertyBagKey(key: nameof(expectedSubject)),
                new PropertyBagValue<string>(expectedSubject))
            .Build(
                new PropertyBagKey(key: nameof(normalizedExpectedSubject)),
                new PropertyBagValue<string>(normalizedExpectedSubject))
            .Build(
                new PropertyBagKey(key: nameof(options)),
                new PropertyBagValue<string>(options?.ToString() ?? "Null"))
            .Build(
                new PropertyBagKey(key: "ActualSubject"),
                new PropertyBagValue<string>(_certificate.Subject));

        if (!textComparer.Compare(_certificate.Subject, normalizedExpectedSubject))
        {
            throw new ValidationException(
                $"Expected SSL certificate subject to be exactly \"{normalizedExpectedSubject}\", but the actual " +
                $"subject was \"{_certificate.Subject}\". This exception occurred as part of validating SSL " +
                $"certificate data.");
        }

        // Create a successful test step with detailed information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return this;
    }

    public ISslCertificateAssertions ToHaveIssuerContaining(string expectedIssuer, TextOptions? options = null)
    {
        var normalizedExpectedIssuer = expectedIssuer.Trim();
        var textComparer = new TextComparer(options);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToHaveIssuerContaining)))
            .Build(
                new PropertyBagKey(key: nameof(expectedIssuer)),
                new PropertyBagValue<string>(expectedIssuer))
            .Build(
                new PropertyBagKey(key: nameof(normalizedExpectedIssuer)),
                new PropertyBagValue<string>(normalizedExpectedIssuer))
            .Build(
                new PropertyBagKey(key: nameof(options)),
                new PropertyBagValue<string>(options?.ToString() ?? "Null"))
            .Build(
                new PropertyBagKey(key: "ActualIssuer"),
                new PropertyBagValue<string>(_certificate.Issuer));

        if (!textComparer.Compare(_certificate.Issuer, normalizedExpectedIssuer))
        {
            throw new ValidationException(
                $"Expected SSL certificate issuer to contain \"{normalizedExpectedIssuer}\", but the actual " +
                $"issuer was \"{_certificate.Issuer}\". This exception occurred as part of validating SSL " +
                $"certificate data.");
        }

        // Create a successful test step with detailed information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return this;
    }

    public ISslCertificateAssertions ToHaveIssuerEqualTo(string expectedIssuer, TextOptions? options = null)
    {
        var normalizedExpectedIssuer = expectedIssuer.Trim();
        // For exact matching, create options with MatchWholeWord = true
        var exactMatchOptions = options != null 
            ? options with { MatchWholeWord = true } 
            : new TextOptions(MatchWholeWord: true);
        var textComparer = new TextComparer(exactMatchOptions);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToHaveIssuerEqualTo)))
            .Build(
                new PropertyBagKey(key: nameof(expectedIssuer)),
                new PropertyBagValue<string>(expectedIssuer))
            .Build(
                new PropertyBagKey(key: nameof(normalizedExpectedIssuer)),
                new PropertyBagValue<string>(normalizedExpectedIssuer))
            .Build(
                new PropertyBagKey(key: nameof(options)),
                new PropertyBagValue<string>(options?.ToString() ?? "Null"))
            .Build(
                new PropertyBagKey(key: "ActualIssuer"),
                new PropertyBagValue<string>(_certificate.Issuer));

        if (!textComparer.Compare(_certificate.Issuer, normalizedExpectedIssuer))
        {
            throw new ValidationException(
                $"Expected SSL certificate issuer to be exactly \"{normalizedExpectedIssuer}\", but the actual " +
                $"issuer was \"{_certificate.Issuer}\". This exception occurred as part of validating SSL " +
                $"certificate data.");
        }

        // Create a successful test step with detailed information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return this;
    }

    public ISslCertificateAssertions ToBeValid()
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToBeValid)))
            .Build(
                new PropertyBagKey(key: "SslErrors"),
                new PropertyBagValue<string>(_sslErrors.ToString()))
            .Build(
                new PropertyBagKey(key: "ChainStatus"),
                new PropertyBagValue<string>(string.Join(", ", _chain.ChainStatus.Select(cs => cs.Status.ToString()))));

        if (_sslErrors != SslPolicyErrors.None)
        {
            throw new ValidationException(
                $"Expected SSL certificate to be valid, but validation failed with errors: {_sslErrors}. " +
                $"This exception occurred as part of validating SSL certificate data.");
        }

        // Create a successful test step with detailed information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return this;
    }

    public ISslCertificateAssertions ToExpireAfter(DateTime minExpirationDate)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToExpireAfter)))
            .Build(
                new PropertyBagKey(key: nameof(minExpirationDate)),
                new PropertyBagValue<string>(minExpirationDate.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)))
            .Build(
                new PropertyBagKey(key: "ActualExpirationDate"),
                new PropertyBagValue<string>(_certificate.NotAfter.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)));

        if (_certificate.NotAfter <= minExpirationDate)
        {
            throw new ValidationException(
                $"Expected SSL certificate to expire after \"{minExpirationDate:yyyy-MM-dd HH:mm:ss}\", but it " +
                $"expires on \"{_certificate.NotAfter:yyyy-MM-dd HH:mm:ss}\". This exception occurred as part of " +
                $"validating SSL certificate data.");
        }

        // Create a successful test step with detailed information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return this;
    }

    public ISslCertificateAssertions ToExpireWithinDays(int days)
    {
        var maxExpirationDate = DateTime.UtcNow.AddDays(days);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToExpireWithinDays)))
            .Build(
                new PropertyBagKey(key: nameof(days)),
                new PropertyBagValue<string>(days.ToString(CultureInfo.InvariantCulture)))
            .Build(
                new PropertyBagKey(key: "MaxExpirationDate"),
                new PropertyBagValue<string>(maxExpirationDate.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)))
            .Build(
                new PropertyBagKey(key: "ActualExpirationDate"),
                new PropertyBagValue<string>(_certificate.NotAfter.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)));

        if (_certificate.NotAfter > maxExpirationDate)
        {
            throw new ValidationException(
                $"Expected SSL certificate to expire within {days} days (by {maxExpirationDate:yyyy-MM-dd HH:mm:ss}), " +
                $"but it expires on \"{_certificate.NotAfter:yyyy-MM-dd HH:mm:ss}\". This exception occurred as part of " +
                $"validating SSL certificate data.");
        }

        // Create a successful test step with detailed information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return this;
    }

    public ISslCertificateAssertions ToHaveThumbprint(string expectedThumbprint, TextOptions? options = null)
    {
        var normalizedExpectedThumbprint = expectedThumbprint.Replace(":", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
        var normalizedActualThumbprint = _certificate.Thumbprint.ToUpperInvariant();

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToHaveThumbprint)))
            .Build(
                new PropertyBagKey(key: nameof(expectedThumbprint)),
                new PropertyBagValue<string>(expectedThumbprint))
            .Build(
                new PropertyBagKey(key: nameof(normalizedExpectedThumbprint)),
                new PropertyBagValue<string>(normalizedExpectedThumbprint))
            .Build(
                new PropertyBagKey(key: "ActualThumbprint"),
                new PropertyBagValue<string>(_certificate.Thumbprint))
            .Build(
                new PropertyBagKey(key: nameof(normalizedActualThumbprint)),
                new PropertyBagValue<string>(normalizedActualThumbprint));

        if (normalizedActualThumbprint != normalizedExpectedThumbprint)
        {
            throw new ValidationException(
                $"Expected SSL certificate thumbprint to be \"{normalizedExpectedThumbprint}\", but the actual " +
                $"thumbprint was \"{normalizedActualThumbprint}\". This exception occurred as part of validating SSL " +
                $"certificate data.");
        }

        // Create a successful test step with detailed information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return this;
    }

    public ISslCertificateAssertions ToHaveSerialNumber(string expectedSerialNumber, TextOptions? options = null)
    {
        var normalizedExpectedSerialNumber = expectedSerialNumber.Trim();
        // For exact matching, create options with MatchWholeWord = true
        var exactMatchOptions = options != null 
            ? options with { MatchWholeWord = true } 
            : new TextOptions(MatchWholeWord: true);
        var textComparer = new TextComparer(exactMatchOptions);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToHaveSerialNumber)))
            .Build(
                new PropertyBagKey(key: nameof(expectedSerialNumber)),
                new PropertyBagValue<string>(expectedSerialNumber))
            .Build(
                new PropertyBagKey(key: nameof(normalizedExpectedSerialNumber)),
                new PropertyBagValue<string>(normalizedExpectedSerialNumber))
            .Build(
                new PropertyBagKey(key: nameof(options)),
                new PropertyBagValue<string>(options?.ToString() ?? "Null"))
            .Build(
                new PropertyBagKey(key: "ActualSerialNumber"),
                new PropertyBagValue<string>(_certificate.SerialNumber));

        if (!textComparer.Compare(_certificate.SerialNumber, normalizedExpectedSerialNumber))
        {
            throw new ValidationException(
                $"Expected SSL certificate serial number to be \"{normalizedExpectedSerialNumber}\", but the actual " +
                $"serial number was \"{_certificate.SerialNumber}\". This exception occurred as part of validating SSL " +
                $"certificate data.");
        }

        // Create a successful test step with detailed information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return this;
    }

    public ISslCertificateAssertions ToHaveSignatureAlgorithm(string expectedAlgorithm, TextOptions? options = null)
    {
        var normalizedExpectedAlgorithm = expectedAlgorithm.Trim();
        var textComparer = new TextComparer(options);
        var actualAlgorithm = _certificate.SignatureAlgorithm.FriendlyName ?? _certificate.SignatureAlgorithm.Value ?? "Unknown";

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToHaveSignatureAlgorithm)))
            .Build(
                new PropertyBagKey(key: nameof(expectedAlgorithm)),
                new PropertyBagValue<string>(expectedAlgorithm))
            .Build(
                new PropertyBagKey(key: nameof(normalizedExpectedAlgorithm)),
                new PropertyBagValue<string>(normalizedExpectedAlgorithm))
            .Build(
                new PropertyBagKey(key: nameof(options)),
                new PropertyBagValue<string>(options?.ToString() ?? "Null"))
            .Build(
                new PropertyBagKey(key: "ActualSignatureAlgorithm"),
                new PropertyBagValue<string>(actualAlgorithm));

        if (!textComparer.Compare(actualAlgorithm, normalizedExpectedAlgorithm))
        {
            throw new ValidationException(
                $"Expected SSL certificate signature algorithm to contain \"{normalizedExpectedAlgorithm}\", but the actual " +
                $"algorithm was \"{actualAlgorithm}\". This exception occurred as part of validating SSL " +
                $"certificate data.");
        }

        // Create a successful test step with detailed information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return this;
    }

    public ISslCertificateAssertions ToHaveSanContaining(string expectedSan, TextOptions? options = null)
    {
        var normalizedExpectedSan = expectedSan.Trim();
        var textComparer = new TextComparer(options);

        // Extract Subject Alternative Names
        var sanExtension = _certificate.Extensions.OfType<X509SubjectAlternativeNameExtension>().FirstOrDefault();
        var sanValues = new List<string>();

        if (sanExtension != null)
        {
            var sanText = sanExtension.Format(false);
            sanValues = sanText.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .ToList();
        }

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToHaveSanContaining)))
            .Build(
                new PropertyBagKey(key: nameof(expectedSan)),
                new PropertyBagValue<string>(expectedSan))
            .Build(
                new PropertyBagKey(key: nameof(normalizedExpectedSan)),
                new PropertyBagValue<string>(normalizedExpectedSan))
            .Build(
                new PropertyBagKey(key: nameof(options)),
                new PropertyBagValue<string>(options?.ToString() ?? "Null"))
            .Build(
                new PropertyBagKey(key: "SanValues"),
                new PropertyBagValue<string>(string.Join(", ", sanValues)));

        if (!sanValues.Any(san => textComparer.Compare(san, normalizedExpectedSan)))
        {
            throw new ValidationException(
                $"Expected SSL certificate Subject Alternative Names to contain \"{normalizedExpectedSan}\", but " +
                $"the actual SAN values were \"{string.Join(", ", sanValues)}\". This exception occurred as part of " +
                $"validating SSL certificate data.");
        }

        // Create a successful test step with detailed information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return this;
    }

    public ISslCertificateAssertions ToHaveKeySize(int expectedKeySize)
    {
        // Use the recommended method to get key size
        var actualKeySize = GetKeySize(_certificate);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToHaveKeySize)))
            .Build(
                new PropertyBagKey(key: nameof(expectedKeySize)),
                new PropertyBagValue<string>(expectedKeySize.ToString(CultureInfo.InvariantCulture)))
            .Build(
                new PropertyBagKey(key: "ActualKeySize"),
                new PropertyBagValue<string>(actualKeySize.ToString(CultureInfo.InvariantCulture)));

        if (actualKeySize != expectedKeySize)
        {
            throw new ValidationException(
                $"Expected SSL certificate key size to be {expectedKeySize} bits, but the actual key size was " +
                $"{actualKeySize} bits. This exception occurred as part of validating SSL certificate data.");
        }

        // Create a successful test step with detailed information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return this;
    }

    private static int GetKeySize(X509Certificate2 certificate)
    {
        // Use the recommended approach to get key size
        using var rsa = certificate.GetRSAPublicKey();
        if (rsa != null)
        {
            return rsa.KeySize;
        }

        using var ecdsa = certificate.GetECDsaPublicKey();
        if (ecdsa != null)
        {
            return ecdsa.KeySize;
        }

        using var dsa = certificate.GetDSAPublicKey();
        if (dsa != null)
        {
            return dsa.KeySize;
        }

        // If we can't determine the key size, return 0
        return 0;
    }
}
