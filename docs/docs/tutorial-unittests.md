# NUnit Integration Guide

This tutorial guides you through creating a .NET unit test project using NUnit test framwork that utilizes the Xping SDK library. You will begin by setting up a NUnit test project and configuring it. Next, you will build upon that foundation by developing tests that incorporate Xping components to validate server responses.

Check out the [samples](https://github.com/xping-dev/sdk/tree/main/samples) folder on our GitHub repository to discover more amazing examples and learn how they can benefit you.

## Prerequisites

- A code editor, such as [Visual Studio Code](https://code.visualstudio.com/) with the [C# extension](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csharp).
- The [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).

Or

- [Visual Studio 2022](https://visualstudio.microsoft.com/downloads/?utm_medium=microsoft&utm_source=learn.microsoft.com&utm_campaign=inline+link&utm_content=download+vs2022) with the _.NET desktop development_ workload installed.
 
## Create the app

- Create a .NET 8 NUnit test project named e.g. "ProductionTests".

- Create a folder named _ProductionTests_ for the project, and then open a command prompt in the new folder.

Run the following command:

```console
dotnet new nunit --framework net8.0 --name "ProductionTests"
```

## Install the Xping.Sdk package

Run the following command:

```console
dotnet add package Xping.Sdk --prerelease
```

The `--prerelease` option is necessary because the library is still in beta.

And build the solution:

```console
dotnet build
```

## Ensure headless browsers are installed

In this guide, we will set up the Xping SDK to utilize the Chromium browser for making HTTP requests and parsing server responses. Xping SDK leverages Playwright library to manage headless browsers. Run the following code to install necessary tools and library.

```
pwsh /bin/Debug/net8.0/playwright.ps1 install
```

If `pwsh` is not available, you will have to [install PowerShell](https://docs.microsoft.com/powershell/scripting/install/installing-powershell).

> [!NOTE] 
> This step is only required if you’re going to use the headless browsers. If you encounter any issues installing the headless browsers, please follow: <a href="https://playwright.dev/dotnet/docs/intro">Playwright for .NET Installation Guide</a>.

## Create `HomePageTests` class

Replace the content of the newly crated `HomePageTests` class with the following code:

```csharp
[TestFixtureSource(typeof(XpingTestFixture))]
public class HomePageTests(TestAgent testAgent) : XpingAssertions
{
    public required TestAgent TestAgent { get; init; } = testAgent;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        TestAgent.UploadToken = "--- Your Dashboard Upload Token ---"; // optional
    }

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

    [Test]
    public async Task VerifyHomePageTitle()
    {
        TestAgent.UsePageValidation(async page =>
        {
            await Expect(page).ToHaveTitleAsync("STORE");
        });

        await RunAsync(new Uri("https://demoblaze.com"));
    }

    private async Task RunAsync(Uri url)
    {
        await using var session = await TestAgent.RunAsync(url);

        Assert.That(session.IsValid, Is.True, session.Failures.FirstOrDefault()?.ErrorMessage);
    }
}
```

The preceding code we added earlier in `HomePageTests.cs` does following:

- Decorates the class with the `[TestFixtureSource(typeof(XpingTestFixture))]` attribute.

Xping SDK library provides test fixture class @Xping.Sdk.XpingTestFixture to streamline the setup and configuration of the Xping SDK. This fixture will provide @Xping.Sdk.Core.TestAgent instance to your class.

- Derive from the @Xping.Sdk.XpingAssertions class.

This class provides static methods such as `Expect` for assertion validation that can be used to make assertions state in the tests.

```csharp
[TestFixtureSource(typeof(XpingTestFixture))]
public class HomePageTests(TestAgent testAgent) : XpingAssertions
{
    public required TestAgent TestAgent { get; init; } = testAgent;

    (...)
}
```

- Implement `OneTimeSetUp`, `SetUp` and `TearDown` methods.

`OneTimeSetUp` is executed once before any of the tests and is particularly useful for setting up any resources. In this case we are setting up @Xping.Sdk.Core.TestAgent class to provide `UploadToken` key. This token facilitates the upload of testing sessions to the server for further analysis.

Two additional methods, `SetUp` and `TearDown`, are invoked before and after each test, respectively. To ensure the browser client is utilized in all tests, it is advisable to configure Xping to do so within the `SetUp` method. Xping will then perform all HTTP requests using a headless browser.

Furthermore, it is recommended to clean up the Xping testing pipeline after each test, and the `TearDown` method is the ideal place for this cleanup process.

```csharp
[OneTimeSetUp]
public void OneTimeSetUp()
{
    TestAgent.UploadToken = "--- Your Dashboard Upload Token ---"; // optional
}

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
```

- Verifies Title of the Home Page

```csharp
[Test]
public async Task VerifyHomePageTitle()
{
    TestAgent.UsePageValidation(async page =>
    {
        await Expect(page).ToHaveTitleAsync("STORE");
    });

    await RunAsync(new Uri("https://demoblaze.com"));
}
```

1. `TestAgent.UsePageValidation` is a method that sets up a validation test step for the page. It takes a lambda function as a parameter, which receives a `IPage` object representing the browser web page being tested.

2. Inside the lambda function, `await Expect(page).ToHaveTitleAsync("STORE")` is used to assert that the title of the page is “STORE”. This is an asynchronous operation that waits for the page title to match the expected value.

3. `await RunAsync(new Uri("https://demoblaze.com"))` initiates the test by navigating to the specified URL (https://demoblaze.com). The test will run the validation pipeline set up earlier once the page is loaded.

This code essentially verifies that the title of the home page at https://demoblaze.com is “STORE”. If the title matches, the test passes; otherwise, it fails.
