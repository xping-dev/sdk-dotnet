using Microsoft.AspNetCore.Mvc.Testing;
using WebApp;
using Xping.Sdk;

namespace IntegrationTesting.TestSuite;

public class TestFixtureProvider : XpingIntegrationTestFixture<Program>
{
    protected override WebApplicationFactory<Program> CreateFactory()
    {
        return new WebAppFactory();
    }
}
