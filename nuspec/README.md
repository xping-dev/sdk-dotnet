## About The Project

**Xping** SDK provides a set of tools to make it easy to write automated tests for Web Application and Web API, as well as troubleshoot issues that may arise during testing. The library provides a number of features to verify that the Web Application is functioning correctly, such as checking that the correct data is displayed on a page or that the correct error messages are displayed when an error occurs.

The library is called **Xping**, which stands for e**X**ternal **Ping**s, and is used to verify the availability of a server and monitor its content. 

You can find more information about the library, including documentation and examples, on the official website [https://xping.io](https://www.xping.io).

<!-- GETTING STARTED -->
## Getting Started

The library is distributed as a [NuGet packages](https://www.nuget.org/profiles/Xping), which can be installed using the [.NET CLI](https://docs.microsoft.com/en-us/dotnet/core/tools/) command `dotnet add package`. Here are the steps to get started:

### Installation using .NET CLI

1. Open a command prompt or terminal window.

2. Navigate to the directory where your project is located.

3. Run the following command to install the **Xping** NuGet package:

   ```
   dotnet add package Xping.Availability
   ```

4. Once the package is installed, you can start using the **Xping** library in your project.

```c#
using Xping.Sdk.Core.DependencyInjection;

Host.CreateDefaultBuilder()
    .ConfigureServices(services =>
    {
        .AddHttpClientFactory()
        .AddTestAgent(agent =>
        {
            agent.UploadToken = "--- Your Dashboard Upload Token ---";
            agent.ApiKey = "--- Your Dashboard API Key ---"; // For authentication with Xping services
            agent.UseDnsLookup()
                 .UseIPAddressAccessibilityCheck()
                 .UseHttpClient()
                 .UseHttpValidation(response =>
                 {
                     Expect(response)
                         .ToHaveSuccessStatusCode()
                         .ToHaveHttpHeader(HeaderNames.Server)
                             .WithValue("Google", new() { Exact = false });
                 })
        });
    });
```

```c#
using Xping.Sdk.Core;
using Xping.Sdk.Core.Session;

var testAgent = _serviceProvider.GetRequiredService<TestAgent>();

TestSession session = await testAgent.RunAsync(new Uri("www.demoblaze.com"));
```

You can also integrate it with your preferred testing framework, such as NUnit, as shown below:

```c#
[TestFixtureSource(typeof(XpingTestFixture))]
public class HomePageTests(TestAgent testAgent) : XpingAssertions
{
    public required TestAgent TestAgent { get; init; } = testAgent;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        TestAgent.UploadToken = "--- Your Dashboard Upload Token ---"; // optional
        TestAgent.ApiKey = "--- Your Dashboard API Key ---"; // For authentication with Xping services
    }

    [Test]
    public async Task VerifyHomePageTitle()
    {
        TestAgent.UseBrowserClient()
                 .UsePageValidation(async page =>
                 {
                     await Expect(page).ToHaveTitleAsync("STORE");
                 });

        await using TestSession session = await TestAgent.RunAsync(new Uri("https://demoblaze.com"));

        Assert.That(session.IsValid, Is.True, session.Failures.FirstOrDefault()?.ErrorMessage);
    }
```

That’s it! You’re now ready to start automating your web application tests and monitoring your server’s content using **Xping**.

## License

Distributed under the MIT License. See `LICENSE` file for more information.
