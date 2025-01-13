/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

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
