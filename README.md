<div id="top"></div>

[![NuGet](https://img.shields.io/nuget/v/Xping.Sdk)](https://www.nuget.org/profiles/Xping)
![Build Status](https://github.com/xping-dev/sdk-dotnet/actions/workflows/ci.yml/badge.svg)
[![codecov](https://codecov.io/gh/xping-dev/sdk-dotnet/graph/badge.svg?token=VUOVI3YUTO)](https://codecov.io/gh/xping-dev/sdk-dotnet)

<!-- PROJECT LOGO -->
<br />
<div align="center">
  <img src="docs/docs/media/logo.svg" />
  <h2 align="center">Xping SDK</h3>
  <p align="center">
    <b>Xping SDK</b> is a free and open-source .NET library written in C# to help automate Web Application or Web API testing.
    <br />
    <br />
    <a href="https://github.com/xping-dev/sdk/issues">Report Bug</a>
    ·
    <a href="https://github.com/xping-dev/sdk/issues">Request Feature</a>
  </p>
</div>


<!-- TABLE OF CONTENTS -->
<details>
  <summary>Table of Contents</summary>
  <ol>
    <li><a href="#about-the-project">About The Project</a></li>
    <li><a href="#getting-started">Getting Started</a>
      <ul>
        <li><a href="#installation-using-.net-cli">Installation using .NET CLI</a></li>
      </ul>
    </li>
    <li><a href="#usage">Usage</a></li>
    <li><a href="#roadmap">Roadmap</a></li>
    <li><a href="#contributing">Contributing</a></li>
    <li><a href="#license">License</a></li>
  </ol>
</details> 


<!-- ABOUT THE PROJECT -->
## About The Project

<b>Xping SDK</b> provides a comprehensive set of tools to simplify writing automated tests for Web Applications and Web APIs, as well as troubleshooting issues during testing. The library offers features to verify the correct functionality of web applications, such as ensuring the correct data is displayed on a page or the appropriate error messages appear when an error occurs.

In addition to these core features, Xping SDK leverages AI-powered testing and live site monitoring techniques to enhance test reliability and performance. This ensures that your web applications are not only functioning correctly but are also monitored for real-time performance and availability.

The library, named Xping, stands for eXternal Pings and is used to verify the availability of a server and monitor its content.

For more information about the library, including documentation and examples, visit the official website <a href="https://xping.io">xping.io</a>.

<p align="right">(<a href="#top">back to top</a>)</p>


<!-- GETTING STARTED -->
## Getting Started

The library is distributed as a [NuGet packages](https://www.nuget.org/profiles/Xping), which can be installed using the [.NET CLI](https://docs.microsoft.com/en-us/dotnet/core/tools/) command `dotnet add package`. Here are the steps to get started:

### Installation using .NET CLI

1. Open a command prompt or terminal window.

2. Navigate to the directory where your project is located.

3. Run the following command to install the <b>Xping</b> NuGet package:

   ```
   dotnet add package Xping.Sdk
   ```

4. Once the package is installed, you can start using the <b>Xping</b> library in your project.

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
                         .ToHaveResponseTimeLessThan(TimeSpan.FromMilliseconds(MaxResponseTimeMs))
                         .ToHaveHeaderWithValue(HeaderNames.Server, "ServerName");
                 })
                 .UseHtmlValidation(html =>
                 {
                     Expect(html).ToHaveTitle("Home page");
                 });
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
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        testAgent.UploadToken = "--- Your Dashboard Upload Token ---";
        testAgent.ApiKey = "--- Your Dashboard API Key ---"; // For authentication with Xping services
    }

    [Test]
    public async Task VerifyHomePageTitle()
    {
        testAgent.UseBrowserClient()
                 .UsePageValidation(async page =>
                 {
                     await Expect(page).ToHaveTitleAsync("STORE");
                 });

        await using TestSession session = await testAgent.RunAsync(new Uri("https://demoblaze.com"));

        Assert.That(session.IsValid, Is.True, session.Failures.FirstOrDefault()?.ErrorMessage);
    }
```

That’s it! You’re now ready to start automating your web application tests and monitoring your server’s content using <b>Xping</b>.

<p align="right">(<a href="#top">back to top</a>)</p>


<!-- USAGE EXAMPLES -->
## Usage

The `samples` folder in this repository contains various examples of how to use Xping SDK for your testing needs. For a comprehensive guide on how to install, configure, and customize Xping SDK, please refer to the [docs](https://xping-dev.github.io/sdk-dotnet/index.html).

<p align="right">(<a href="#top">back to top</a>)</p>


<!-- CONFIGURATION -->
## Configuration

### Authentication

Xping SDK supports authentication with Xping services using an API key. This key is used to authenticate requests when uploading test results to your dashboard.

#### Setting the API Key

You can set the API key in two ways:

**Method 1: Explicitly in code**
```csharp
services.AddTestAgent(agent =>
{
    agent.ApiKey = "your-api-key-here";
});
```

**Method 2: Using environment variable (recommended for CI/CD)**
```bash
export XPING_API_KEY="your-api-key-here"
```

When using environment variables, you don't need to set the API key explicitly in your code. The SDK will automatically read the value from the `XPING_API_KEY` environment variable.

**Note:** If you set the API key explicitly in code, it will take precedence over the environment variable.

<p align="right">(<a href="#top">back to top</a>)</p>


<!-- ROADMAP -->
## Roadmap

We use [Milestones](https://github.com/xping-dev/sdk-dotnet/milestones) to communicate upcoming changes in <b>Xping</b> SDK:

- [Working Set](https://github.com/xping-dev/sdk-dotnet/milestone/1) refers to the features that are currently being actively worked on. While not all of these features will be committed in the next release, they do reflect the top priorities of the maintainers for the upcoming period.

- [Backlog](https://github.com/xping-dev/sdk-dotnet/milestone/2) is a set of feature candidates for some future releases, but are not being actively worked on.

<p align="right">(<a href="#top">back to top</a>)</p>


<!-- CONTRIBUTING -->
## Contributing

Contributions are what make the open source community such an amazing place to learn, inspire, and create. Any contributions you make are **greatly appreciated**.

If you have a suggestion that would make this better, please fork the repo and create a pull request. You can also simply open an issue with the tag "enhancement".
Don't forget to give the project a star! Thanks again!

1. Fork the Project
2. Create your Feature Branch (`git checkout -b feature/AmazingFeature`)
3. Commit your Changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the Branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

<p align="right">(<a href="#top">back to top</a>)</p>


<!-- LICENSE -->
## License

Distributed under the MIT License. See `LICENSE` file for more information.

<p align="right">(<a href="#top">back to top</a>)</p>
