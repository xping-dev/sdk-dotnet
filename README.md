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
    <b>Xping</b> brings observability to testing. We help developers and teams understand not just whether tests pass, but whether they can be trusted. Our mission is to eliminate wasted time on flaky tests and provide actionable insights that improve test reliability and confidence.
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


## Architecture Overview

### Three-Layer Architecture:

- SDK Core Library – Shared execution tracking logic
- Test Framework Adapters – NUnit, xUnit, MSTest integrations
- Data Uploader – API client for sending results to Xping platform

### Technology Stack

<b>SDK Development</b>

- .NET Standard 2.0 – Ensures compatibility across .NET Framework, .NET Core, and .NET 5+
- C# – Primary language
- NuGet – Package distribution

<b>Test Framework Integration</b>

- NUnit – [OneTimeSetUp], [OneTimeTearDown], and TestContext for hooks
- xUnit – ITestOutputHelper and custom ITestFramework implementation
- MSTest – [AssemblyInitialize], [AssemblyCleanup], and TestContext

<b>Data Collection & Storage</b>

- System.Diagnostics – For capturing environment metadata (OS, runtime version)
- JSON Serialization – System.Text.Json for lightweight serialization
- In-Memory Queue – Buffer test results before batch upload

<b>API Communication</b>

- HttpClient – Async HTTP client for uploading results
- Polly – Retry policies and circuit breaker for resilient API calls
- Compression – Gzip compression for payload optimization

<b>Configuration</b>

- appsettings.json / environment variables – API endpoint, API key, batch size
- Microsoft.Extensions.Configuration – Configuration provider

<p align="right">(<a href="#top">back to top</a>)</p>

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

## Roadmap

We use [Milestones](https://github.com/xping-dev/sdk-dotnet/milestones) to communicate upcoming changes in <b>Xping</b> SDK:

- [Working Set](https://github.com/xping-dev/sdk-dotnet/milestone/1) refers to the features that are currently being actively worked on. While not all of these features will be committed in the next release, they do reflect the top priorities of the maintainers for the upcoming period.

- [Backlog](https://github.com/xping-dev/sdk-dotnet/milestone/2) is a set of feature candidates for some future releases, but are not being actively worked on.

<p align="right">(<a href="#top">back to top</a>)</p>

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

## License

Distributed under the MIT License. See `LICENSE` file for more information.

<p align="right">(<a href="#top">back to top</a>)</p>
