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
                new PropertyBagValue<string>($"{nameof(SslCertificateAssertions)}.{nameof(ToHaveSubjectContaining)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"To have subject containing: {normalizedExpectedSubject}"))
            .Build(
                new PropertyBagKey(key: nameof(TextOptions)),
                new PropertyBagValue<string>(options?.ToString() ?? "Null"));

        if (!textComparer.Compare(_certificate.Subject, normalizedExpectedSubject))
        {
            throw new ValidationException(
                $"Expected SSL certificate subject to contain \"{normalizedExpectedSubject}\", but the actual " +
                $"subject was \"{_certificate.Subject}\".");
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
                new PropertyBagValue<string>($"{nameof(SslCertificateAssertions)}.{nameof(ToHaveSubjectEqualTo)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"To have subject equal to: {normalizedExpectedSubject}"))
            .Build(
                new PropertyBagKey(key: nameof(TextOptions)),
                new PropertyBagValue<string>(options?.ToString() ?? "Null"));

        if (!textComparer.Compare(_certificate.Subject, normalizedExpectedSubject))
        {
            throw new ValidationException(
                $"Expected SSL certificate subject to be exactly \"{normalizedExpectedSubject}\", but the actual " +
                $"subject was \"{_certificate.Subject}\".");
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
                new PropertyBagValue<string>($"{nameof(SslCertificateAssertions)}.{nameof(ToHaveIssuerContaining)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"To have issuer containing: {normalizedExpectedIssuer}"))
            .Build(
                new PropertyBagKey(key: nameof(TextOptions)),
                new PropertyBagValue<string>(options?.ToString() ?? "Null"));

        if (!textComparer.Compare(_certificate.Issuer, normalizedExpectedIssuer))
        {
            throw new ValidationException(
                $"Expected SSL certificate issuer to contain \"{normalizedExpectedIssuer}\", but the actual " +
                $"issuer was \"{_certificate.Issuer}\".");
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
                new PropertyBagValue<string>($"{nameof(SslCertificateAssertions)}.{nameof(ToHaveIssuerEqualTo)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"To have issuer equal to: {normalizedExpectedIssuer}"))
            .Build(
                new PropertyBagKey(key: nameof(TextOptions)),
                new PropertyBagValue<string>(options?.ToString() ?? "Null"));

        if (!textComparer.Compare(_certificate.Issuer, normalizedExpectedIssuer))
        {
            throw new ValidationException(
                $"Expected SSL certificate issuer to be exactly \"{normalizedExpectedIssuer}\", but the actual " +
                $"issuer was \"{_certificate.Issuer}\".");
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
                new PropertyBagValue<string>($"{nameof(SslCertificateAssertions)}.{nameof(ToBeValid)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("To be valid"))
            .Build(
                new PropertyBagKey(key: "SslErrors"),
                new PropertyBagValue<string>(_sslErrors.ToString()))
            .Build(
                new PropertyBagKey(key: "ChainStatus"),
                new PropertyBagValue<string>(string.Join(", ", _chain.ChainStatus.Select(cs => cs.Status.ToString()))));

        if (_sslErrors != SslPolicyErrors.None)
        {
            throw new ValidationException(
                $"Expected SSL certificate to be valid, but validation failed with errors: {_sslErrors}.");
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
                new PropertyBagValue<string>($"{nameof(SslCertificateAssertions)}.{nameof(ToExpireAfter)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"To expire after: {minExpirationDate:yyyy-MM-dd HH:mm:ss}"))
            .Build(
                new PropertyBagKey(key: "MinExpirationDate"),
                new PropertyBagValue<string>(minExpirationDate.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)));

        if (_certificate.NotAfter <= minExpirationDate)
        {
            throw new ValidationException(
                $"Expected SSL certificate to expire after \"{minExpirationDate:yyyy-MM-dd HH:mm:ss}\", but it " +
                $"expires on \"{_certificate.NotAfter:yyyy-MM-dd HH:mm:ss}\".");
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
                new PropertyBagValue<string>($"{nameof(SslCertificateAssertions)}.{nameof(ToExpireWithinDays)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"To expire within {days} days"))
            .Build(
                new PropertyBagKey(key: "Days"),
                new PropertyBagValue<string>(days.ToString(CultureInfo.InvariantCulture)));

        if (_certificate.NotAfter > maxExpirationDate)
        {
            throw new ValidationException(
                $"Expected SSL certificate to expire within {days} days (by {maxExpirationDate:yyyy-MM-dd HH:mm:ss}), " +
                $"but it expires on \"{_certificate.NotAfter:yyyy-MM-dd HH:mm:ss}\".");
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
                new PropertyBagValue<string>($"{nameof(SslCertificateAssertions)}.{nameof(ToHaveThumbprint)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"To have thumbprint: {normalizedExpectedThumbprint}"))
            .Build(
                new PropertyBagKey(key: nameof(TextOptions)),
                new PropertyBagValue<string>(options?.ToString() ?? "Null"));

        if (normalizedActualThumbprint != normalizedExpectedThumbprint)
        {
            throw new ValidationException(
                $"Expected SSL certificate thumbprint to be \"{normalizedExpectedThumbprint}\", but the actual " +
                $"thumbprint was \"{normalizedActualThumbprint}\".");
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
                new PropertyBagValue<string>($"{nameof(SslCertificateAssertions)}.{nameof(ToHaveSerialNumber)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"To have serial number: {normalizedExpectedSerialNumber}"))
            .Build(
                new PropertyBagKey(key: nameof(TextOptions)),
                new PropertyBagValue<string>(options?.ToString() ?? "Null"));

        if (!textComparer.Compare(_certificate.SerialNumber, normalizedExpectedSerialNumber))
        {
            throw new ValidationException(
                $"Expected SSL certificate serial number to be \"{normalizedExpectedSerialNumber}\", but the actual " +
                $"serial number was \"{_certificate.SerialNumber}\".");
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
                new PropertyBagValue<string>($"{nameof(SslCertificateAssertions)}.{nameof(ToHaveSignatureAlgorithm)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"To have signature algorithm containing: {normalizedExpectedAlgorithm}"))
            .Build(
                new PropertyBagKey(key: nameof(TextOptions)),
                new PropertyBagValue<string>(options?.ToString() ?? "Null"));

        if (!textComparer.Compare(actualAlgorithm, normalizedExpectedAlgorithm))
        {
            throw new ValidationException(
                $"Expected SSL certificate signature algorithm to contain \"{normalizedExpectedAlgorithm}\", but the actual " +
                $"algorithm was \"{actualAlgorithm}\".");
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
                new PropertyBagValue<string>($"{nameof(SslCertificateAssertions)}.{nameof(ToHaveSanContaining)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"To have SAN containing: {normalizedExpectedSan}"))
            .Build(
                new PropertyBagKey(key: nameof(TextOptions)),
                new PropertyBagValue<string>(options?.ToString() ?? "Null"));

        if (!sanValues.Any(san => textComparer.Compare(san, normalizedExpectedSan)))
        {
            throw new ValidationException(
                $"Expected SSL certificate Subject Alternative Names to contain \"{normalizedExpectedSan}\", but " +
                $"the actual SAN values were \"{string.Join(", ", sanValues)}\".");
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
                new PropertyBagValue<string>($"{nameof(SslCertificateAssertions)}.{nameof(ToHaveKeySize)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"To have key size: {expectedKeySize} bits"))
            .Build(
                new PropertyBagKey(key: "ExpectedKeySize"),
                new PropertyBagValue<string>(expectedKeySize.ToString(CultureInfo.InvariantCulture)));

        if (actualKeySize != expectedKeySize)
        {
            throw new ValidationException(
                $"Expected SSL certificate key size to be {expectedKeySize} bits, but the actual key size was " +
                $"{actualKeySize} bits.");
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
