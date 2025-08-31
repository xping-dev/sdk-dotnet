# Xping SDK Samples

This folder contains various examples of how to use Xping SDK for your testing needs. Xping is a powerful and flexible testing framework that allows you to test web applications, APIs, and more. You can learn more about Xping at the [docs](https://xping-dev.github.io/sdk/index.html).

<!-- TABLE OF CONTENTS -->
<details>
  <summary>Table of Contents</summary>
  <ol>
    <li><a href="#prerequisites">Prerequisites</a></li>
    <li><a href="#installation">Installation</a></li>
    <li><a href="#usage">Usage</a></li>
    <li><a href="#samples">Samples</a></li>
    <ul>
        <li><a href="#consoleapptesting-sample">ConsoleApp Testing</a></li>
        <li><a href="#integrationtesting-sample">Integration Testing</a></li>
        <li><a href="#productiontesting-sample">Production Testing</a></li>
        <li><a href="#sslcertificatesample-sample">SSL Certificate Sample</a></li>
    </ul>
  </ol>
</details>

## Prerequisites
Before you can use the samples in this folder, you need to have the following:

- [ ] Visual Studio Code or any other code editor
- [ ] .NET 8 or higher
- [ ] A web application or API to test

## Installation

To install and set up the samples in this folder, follow these steps:

1. Clone the Xping SDK GitHub repository to your local machine:

```
git clone https://github.com/xping-dev/sdk.git
```

2. From the root folder of this repository, restore the NuGet packages:

```
dotnet restore xping-sdk.sln
```

4. Build the solution:

```
dotnet build
```

5. Ensure headless browsers are installed:

```
pwsh samples/ConsoleAppTesting/bin/Debug/net8.0/playwright.ps1 install
```

If `pwsh` is not available, you will have to [install PowerShell](https://docs.microsoft.com/powershell/scripting/install/installing-powershell).

> [!NOTE] 
> This step is only required if you’re going to use the headless browsers in the selected sample. In Xping SDK we leverage Playwright library to manage headless browsers. If you encounter any issues installing the headless browsers, please follow: <a href="https://playwright.dev/dotnet/docs/intro">Playwright for .NET Installation Guide</a>.

## Usage

To run and use the samples, follow these steps:

1. Open the solution file (xping-sdk.sln) in Visual Studio Code or any other code editor.
2. Select the sample project that you want to run from the solution explorer. For example, `ConsoleAppTesting`.
3. For debugging purposes open the `Properties/launchSettings.json` file and modify the settings according to your testing needs. For example, you can change the `--url` of the page being validated. Run the sample project by pressing F5 or using the `dotnet run` command.
Observe the test results in the console or in the Test Explorer window (if you are running `IntegrationTesting` sample).

Here is a screenshot of the `ConsoleAppTesting` sample project running in Visual Studio Code:

![VS Code Running ConsoleAppTesting](/docs/docs/media/vs-code-sample.png)

## Samples

This folder contains the following sample projects:

|Project|Description|
|-------|-----------|
|ConsoleAppTesting|This sample demonstrates you how to use the `Xping.Sdk` NuGet package to test a Web Application or a Web API. It runs as a standalone application and gets a web address of the web application to validate from the command line argument `--url`. This kind of testing can help you test multiple web addresses with the same set of test components, verify if the web application is up and running after deployment, or warm up the web application by hitting its different routes. This sample has more details and features that you can explore by clicking <a href="#consoleapptesting-sample">here</a>.|
|IntegrationTesing|This sample shows you how to test a Web Application or a Web API using the `Xping.Sdk` NuGet package and the `HttpClient` client. It performs integration tests where you can serve the Web Application in memory for testing. This testing will verify if for example the database and the web pages work together correctly and display the right data. This sample has more details and features that you can explore by clicking <a href="#integrationtesting-sample">here</a>.|
| ProductionTesting | This sample demonstrates how to perform testing on a live production environment using the Xping.Sdk NuGet package. It includes techniques for live site monitoring, real-time data validation, and ensuring the overall health and availability of your Web Application or Web API in a production setting. This approach helps to detect and resolve issues promptly, enhancing the reliability and performance of your application. Explore more details and features by clicking <a href="#productiontesting-sample">here</a>.|
|SslCertificateSample|This sample demonstrates how to capture and validate SSL certificates using the Xping SDK. It shows you how to use SSL certificate capture functionality to extract certificate information during HTTPS requests, validate certificate properties (such as expiration dates, subjects, and issuers), and ensure the security compliance of your web applications. This sample uses the `UseSslCertificateCapture()` and `UseSslCertificateValidation()` methods to implement comprehensive SSL certificate testing. This sample has more details and features that you can explore by clicking <a href="#sslcertificatesample-sample">here</a>.|
|WebApp|This project is a simple ASP.NET Core Web Application that serves as a testing target for the `IntegrationTesting`. It has a home page that displays a welcome message and login page. You can use this project to test the functionality and integrity of the Web Application using different test components and scenarios. You can also modify the Web Application to create your own testing targets.|

### ConsoleAppTesting Sample 

`ConsoleAppTesting` has the following features and benefits:

- It runs as a standalone application, which means you don't need to install or configure any other software or tools. You can run it on any machine that has .NET 8.0 or higher installed.
- It retrieves a web address of the web application to validate from the command line argument `--url`, which makes it easy to use and customize. You can provide any valid web address that you want to test, such as https://example.com or https://example.com/?q=query.
- It tests multiple web addresses with the same set of test components, which saves you time and effort. You can register the components for the testing pipeline in the code and reuse them for different web addresses. For example, you can register the `DnsLookup`, `IPAddressAccessibilityCheck`, `HttpRequestSender`, `HttpResponseContentValidator`, and `HttpResponseHeadersValidator` components to test the response content and status code of the web addresses.
- It verifies if the web application is up and running after deployment, which ensures the quality and reliability of your product. You can set the expected response content and status code in the code and compare them with the actual response from the web address. For example, you can set the expected response content to `"Welcome to Xping"` and the expected response status code to `200` and check if they match the actual response from the web address.
- It warms up the web application by hitting different routes, which improves the performance and user experience of your product right after deployment. You can provide web addresses with different paths that access different parts of your web application and make sure they are loaded and ready to use. For example, you can provide web addresses like https://example.com/home, https://example.com/about, and https://example.com/contact.

We hope this project helps you with your testing needs. If you have any feedback or questions, please let us know [here](https://github.com/xping-dev/sdk-dotnet/discussions/1). 😊

### IntegrationTesting Sample

With this project, you can:

- Use the `Xping.Sdk` NuGet package, which is a powerful and flexible testing framework that allows you to test web applications, APIs, and more with the `HttpClient` client, which is a modern and efficient way to make HTTP requests and handle responses.
- Serve the Web Application in memory, which means you don't need to deploy or host the web application on a server. You can run it on any machine that has .NET 8.0 or higher installed.
- Test the Web Application and for example database together, which means you can check the functionality and integrity of the whole system. You can set the expected response content in the code and compare them with the actual response from the web application and the database.

> [!NOTE] 
> Running the Web Application in memory prevents you from using headless browsers in this testing scenario.

We hope this project helps you with your testing needs. If you have any feedback or questions, please let us know [here](https://github.com/xping-dev/sdk-dotnet/discussions/1). 😊

### ProductionTesting Sample

Production Testing includes the following features and benefits:

- It performs testing on a live production environment, ensuring that your web application or API is functioning correctly in real-world conditions. This sample demonstrates how to utilize the Xping.Sdk NuGet package for seamless integration.
- It employs live site monitoring techniques, which allow you to continuously verify the availability and performance of your web application. You can detect and respond to issues in real-time, enhancing the reliability and user experience of your product.
- It validates real-time data and responses, ensuring that the live site delivers accurate and expected content. This helps in maintaining the integrity of your application’s data and interactions.
- It automates the monitoring and testing process, which saves you time and effort. You can set up periodic checks and alerts to notify you of any deviations or failures, allowing for prompt resolution.

We hope this project helps you with your testing needs. If you have any feedback or questions, please let us know [here](https://github.com/xping-dev/sdk-dotnet/discussions/1). 😊

### SslCertificateSample Sample

SSL Certificate Sample includes the following features and benefits:

- It demonstrates SSL certificate capture and validation using the Xping SDK, ensuring that your HTTPS-enabled web applications have valid and secure certificates. This sample shows how to utilize the `UseSslCertificateCapture()` and `UseSslCertificateValidation()` methods for comprehensive SSL testing.
- It captures SSL certificate information during HTTPS requests, allowing you to extract detailed certificate data such as subject, issuer, expiration dates, and certificate chain information. This helps in monitoring certificate health and compliance.
- It validates certificate properties automatically, including checking for certificate validity, expiration dates, and trusted certificate authorities. You can ensure that your web applications are using properly configured and up-to-date SSL certificates.
- It provides real-time SSL certificate monitoring, which helps you detect certificate issues before they affect end users. You can set up alerts for certificates that are expiring soon or have validation issues.
- It integrates seamlessly with your existing testing pipeline, allowing you to include SSL certificate validation as part of your automated testing process. This ensures that certificate-related issues are caught early in the development lifecycle.
- It supports custom validation logic, allowing you to implement specific certificate requirements based on your organization's security policies and compliance needs.

This sample is particularly useful for:
- Security compliance testing and audits
- Certificate expiration monitoring
- Validating certificate chain integrity
- Ensuring proper SSL/TLS configuration
- Automated security testing in CI/CD pipelines

We hope this project helps you with your testing needs. If you have any feedback or questions, please let us know [here](https://github.com/xping-dev/sdk-dotnet/discussions/1). 😊
