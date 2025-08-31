/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using Xping.Sdk.Validations.TextUtils;

namespace Xping.Sdk.Validations.SslCertificate;

/// <summary>
/// Represents an object for validating SSL certificate information.
/// </summary>
public interface ISslCertificateAssertions
{
    /// <summary>
    /// Validates that the SSL certificate subject contains the specified text.
    /// </summary>
    /// <param name="expectedSubject">The expected subject text.</param>
    /// <param name="options">Optional parameter for customizing the search text behavior.</param>
    /// <returns>An instance of <see cref="ISslCertificateAssertions"/> to allow for further assertions.</returns>
    ISslCertificateAssertions ToHaveSubjectContaining(string expectedSubject, TextOptions? options = null);

    /// <summary>
    /// Validates that the SSL certificate subject is equal to the specified value.
    /// </summary>
    /// <param name="expectedSubject">The expected subject value.</param>
    /// <param name="options">Optional parameter for customizing the search text behavior.</param>
    /// <returns>An instance of <see cref="ISslCertificateAssertions"/> to allow for further assertions.</returns>
    ISslCertificateAssertions ToHaveSubjectEqualTo(string expectedSubject, TextOptions? options = null);

    /// <summary>
    /// Validates that the SSL certificate issuer contains the specified text.
    /// </summary>
    /// <param name="expectedIssuer">The expected issuer text.</param>
    /// <param name="options">Optional parameter for customizing the search text behavior.</param>
    /// <returns>An instance of <see cref="ISslCertificateAssertions"/> to allow for further assertions.</returns>
    ISslCertificateAssertions ToHaveIssuerContaining(string expectedIssuer, TextOptions? options = null);

    /// <summary>
    /// Validates that the SSL certificate issuer is equal to the specified value.
    /// </summary>
    /// <param name="expectedIssuer">The expected issuer value.</param>
    /// <param name="options">Optional parameter for customizing the search text behavior.</param>
    /// <returns>An instance of <see cref="ISslCertificateAssertions"/> to allow for further assertions.</returns>
    ISslCertificateAssertions ToHaveIssuerEqualTo(string expectedIssuer, TextOptions? options = null);

    /// <summary>
    /// Validates that the SSL certificate is currently valid (not expired and not before valid date).
    /// </summary>
    /// <returns>An instance of <see cref="ISslCertificateAssertions"/> to allow for further assertions.</returns>
    ISslCertificateAssertions ToBeValid();

    /// <summary>
    /// Validates that the SSL certificate expires after the specified date.
    /// </summary>
    /// <param name="minimumExpiryDate">The minimum expiry date.</param>
    /// <returns>An instance of <see cref="ISslCertificateAssertions"/> to allow for further assertions.</returns>
    ISslCertificateAssertions ToExpireAfter(DateTime minimumExpiryDate);

    /// <summary>
    /// Validates that the SSL certificate expires within the specified number of days from now.
    /// </summary>
    /// <param name="days">The number of days from now.</param>
    /// <returns>An instance of <see cref="ISslCertificateAssertions"/> to allow for further assertions.</returns>
    ISslCertificateAssertions ToExpireWithinDays(int days);

    /// <summary>
    /// Validates that the SSL certificate has the specified thumbprint.
    /// </summary>
    /// <param name="expectedThumbprint">The expected certificate thumbprint.</param>
    /// <param name="options">Optional parameter for customizing the search text behavior.</param>
    /// <returns>An instance of <see cref="ISslCertificateAssertions"/> to allow for further assertions.</returns>
    ISslCertificateAssertions ToHaveThumbprint(string expectedThumbprint, TextOptions? options = null);

    /// <summary>
    /// Validates that the SSL certificate has the specified serial number.
    /// </summary>
    /// <param name="expectedSerialNumber">The expected serial number.</param>
    /// <param name="options">Optional parameter for customizing the search text behavior.</param>
    /// <returns>An instance of <see cref="ISslCertificateAssertions"/> to allow for further assertions.</returns>
    ISslCertificateAssertions ToHaveSerialNumber(string expectedSerialNumber, TextOptions? options = null);

    /// <summary>
    /// Validates that the SSL certificate uses the specified signature algorithm.
    /// </summary>
    /// <param name="expectedAlgorithm">The expected signature algorithm.</param>
    /// <param name="options">Optional parameter for customizing the search text behavior.</param>
    /// <returns>An instance of <see cref="ISslCertificateAssertions"/> to allow for further assertions.</returns>
    ISslCertificateAssertions ToHaveSignatureAlgorithm(string expectedAlgorithm, TextOptions? options = null);

    /// <summary>
    /// Validates that the SSL certificate has a subject alternative name (SAN) containing the specified DNS name.
    /// </summary>
    /// <param name="expectedDnsName">The expected DNS name in the SAN extension.</param>
    /// <param name="options">Optional parameter for customizing the search text behavior.</param>
    /// <returns>An instance of <see cref="ISslCertificateAssertions"/> to allow for further assertions.</returns>
    ISslCertificateAssertions ToHaveSanContaining(string expectedDnsName, TextOptions? options = null);

    /// <summary>
    /// Validates that the SSL certificate has the specified key size.
    /// </summary>
    /// <param name="expectedKeySize">The expected key size in bits.</param>
    /// <returns>An instance of <see cref="ISslCertificateAssertions"/> to allow for further assertions.</returns>
    ISslCertificateAssertions ToHaveKeySize(int expectedKeySize);
}
