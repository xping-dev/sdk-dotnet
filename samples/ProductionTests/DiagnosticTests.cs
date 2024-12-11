using System.Net;
using Microsoft.Playwright;
using Xping.Sdk;
using Xping.Sdk.Core;
using Xping.Sdk.Extensions;

namespace ProductionTests;

[TestFixtureSource(typeof(XpingTestFixture))]
public class DiagnosticTests(TestAgent testAgent) : BaseTest(testAgent)
{
    private const int MaxHtmlSizeAllowed = 150 * 1024; // 150 KB in bytes
    private const int MaxResponseTimeMs = 3000; // 3 seconds in milliseconds
    
    [SetUp]
    public void SetUp()
    {
        TestAgent.UseBrowserClient(options =>
        {
            options.BrowserType = BrowserType.Chromium;
        });
    }

    [TearDown]
    public void TearDown()
    {
        TestAgent.Cleanup();
    }

    [Ignore("Investigate: An error occurred while sending the ping request on CI runner.")]
    [Test]
    public async Task VerifyDnsAndIpAddressAvailability()
    {
        TestAgent
            .UseDnsLookup()
            .UseIPAddressAccessibilityCheck();

        await RunAsync();
    }

    [Test]
    [TestCase("/")] // home page
    [TestCase("/prod.html?idp_=1")] // product page
    public async Task VerifyHttpStatusCode200(string relativeAddress)
    {
        TestAgent.UseHttpValidation(response =>
        {
            Expect(response).ToHaveStatusCode(HttpStatusCode.OK);
        });

        await RunAsync(relativeAddress);
    }
    
    [Test]
    [TestCase("/")] // home page
    [TestCase("/prod.html?idp_=1")] // product page
    public async Task VerifyResponseTimeIsAcceptable(string relativeAddress)
    {
        TestAgent.UseHttpValidation(response =>
        {
            Expect(response)
                .ToHaveResponseTimeLessThan(TimeSpan.FromMilliseconds(MaxResponseTimeMs));
        });

        await RunAsync(relativeAddress);
    }
    
    [Test]
    [TestCase("/")] // home page
    [TestCase("/prod.html?idp_=1")] // product page
    public async Task VerifyMaxHtmlSizeAllowed(string relativeAddress)
    {
        TestAgent.UseHtmlValidation(html =>
        {
            Expect(html).ToHaveMaxDocumentSize(MaxHtmlSizeAllowed);
        });

        await RunAsync(relativeAddress);
    }
}