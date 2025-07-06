/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using Microsoft.Playwright;
using Xping.Sdk;
using Xping.Sdk.Core;
using Xping.Sdk.Extensions;

namespace ProductionTests;

[TestFixtureSource(typeof(XpingTestFixture))]
public class HomePageTests(TestAgent testAgent) : BaseTest(testAgent)
{
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

    [Test(Description = "Verifies that home page has correct title 'STORE'")]
    public async Task VerifyHomePageTitle()
    {
        TestAgent.UsePageValidation(async page =>
        {
            await Expect(page).ToHaveTitleAsync("STORE");
        });

        await RunAsync();
    }

    [Test(Description = "Verifies that home page has correct categories")]
    public async Task VerifyCategories()
    {
        const int expectedCount = 4;
        
        TestAgent.UsePageValidation(async page =>
        {
            var categories = page.Locator(selector: "//div[@class='list-group']/a");
            
            await Expect(categories).ToHaveCountAsync(expectedCount);
            await Expect(categories).ToHaveTextAsync(["CATEGORIES", "Phones", "Laptops", "Monitors"]);
        });
        
        await RunAsync();
    }

    [Test]
    public async Task VerifyNavigationBar()
    {
        TestAgent.UsePageValidation(async page =>
        {
            await Expect(page.GetByRole(AriaRole.Link, new() { Name = "Home (current)" })).ToBeVisibleAsync();
            await Expect(page.GetByRole(AriaRole.Link, new() { Name = "About us" })).ToBeVisibleAsync();
            await Expect(page.GetByRole(AriaRole.Link, new() { Name = "Contact" })).ToBeVisibleAsync();
            await Expect(page.GetByRole(AriaRole.Link, new() { Name = "Cart" })).ToBeVisibleAsync();
            await Expect(page.GetByRole(AriaRole.Link, new() { Name = "Log in" })).ToBeVisibleAsync();
            await Expect(page.GetByRole(AriaRole.Link, new() { Name = "Sign up" })).ToBeVisibleAsync();
        });
        
        await RunAsync();
    }
}
