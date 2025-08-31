/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using Xping.Sdk;
using Xping.Sdk.Core;
using Xping.Sdk.Extensions;

namespace ProductionTests;

[TestFixtureSource(typeof(XpingTestFixture))]
public class SslCertificateTests(TestAgent testAgent) : BaseTest(testAgent)
{
    [SetUp]
    public void SetUp()
    {
        TestAgent
            .UseSslCertificateCapture()
            .UseHttpClient();
    }

    [TearDown]
    public void TearDown()
    {
        TestAgent.Cleanup();
    }

    [Test(Description = "Capture SSL certificate from HTTPS endpoint")]
    [Xping("ssl-certificate-capture-test")]
    public async Task SslCertificateShouldBeValid()
    {
        TestAgent.UseSslCertificateValidation(ssl =>
        {
            Expect(ssl).ToBeValid();
        });

        await RunAsync();
    }
}
