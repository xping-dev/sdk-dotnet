using System.Xml.XPath;
using IntegrationTesting.TestSuite;
using Xping.Sdk.Core;
using Xping.Sdk.Extensions;

namespace IntegrationTesting;

[TestFixtureSource(typeof(TestFixtureProvider))]
public class HomePageTests(TestAgent testAgent) : BaseTest(testAgent) 
{
    [SetUp]
    public void SetUp()
    {
        TestAgent.UseHttpClient();
    }

    [TearDown]
    public void TearDown()
    {
        TestAgent.Cleanup();
    }

    [Test]
    public async Task EnsureSuccessHttpStatusCode()
    {
        // Arrange
        TestAgent.UseHttpValidation(response =>
        {
            // Assert
            Expect(response).ToHaveSuccessStatusCode();
        });

        // Act
        await RunAsync();
    }

    [Test]
    public async Task VerifyMaxDocumentSize()
    {
        // Arrange
        TestAgent.UseHtmlValidation(html =>
        {
            // Assert
            Expect(html).ToHaveMaxDocumentSize(maxSizeInBytes: 153600); // 150kB)
        });

        await RunAsync();
    }

    [Test]
    public async Task VerifyHomePageTitle()
    {
        // Arrange
        TestAgent.UseHtmlValidation(html =>
        {
            // Assert
            Expect(html).ToHaveTitle("Home page - WebApp");
        });

        // Act
        await RunAsync();
    }

    [Test]
    public async Task VerifyCategories()
    {
        const int expectedCount = 4;
        
        TestAgent.UseHtmlValidation(html =>
        {
            var categories = html.Locator(selector: "//div[@class='container']//li");
            
            Expect(categories).ToHaveCount(expectedCount);
            Expect(categories).ToHaveInnerText(["Home", "Privacy", "Register", "Login"]);
        });
        
        await RunAsync();
    }
}
