/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using Xping.Sdk.Core.Components;
using Xping.Sdk.Shared;

namespace Xping.Sdk.Validations.SslCertificate.Internals;

internal class SslCertificateInfo(
    X509Certificate2 certificate,
    X509Chain chain,
    SslPolicyErrors sslErrors,
    TestContext context) : ISslCertificateResponse
{
    private readonly X509Certificate2 _certificate = certificate.RequireNotNull(nameof(certificate));
    private readonly X509Chain _chain = chain.RequireNotNull(nameof(chain));
    private readonly TestContext _context = context.RequireNotNull(nameof(context));
    private readonly SslPolicyErrors _sslErrors = sslErrors;

    public X509Certificate2 Certificate => _certificate;
    public X509Chain Chain => _chain;
    public SslPolicyErrors SslErrors => _sslErrors;
    public TestContext Context => _context;
}
