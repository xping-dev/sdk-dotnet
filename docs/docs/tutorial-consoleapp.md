# Console Application Guide

This tutorial demonstrates how to create a .NET console application that utilizes the Xping SDK library. You will start by creating a basic test agent and adding a reporting service. Then, you will build upon that foundation by creating a validation pipeline that contains multiple test steps which will run test operations to validate server response. 

Check out the [samples](https://github.com/xping-dev/sdk/tree/main/samples) folder on our GitHub repository to discover more amazing examples and learn how they can benefit you.

## Prerequisites

- A code editor, such as [Visual Studio Code](https://code.visualstudio.com/) with the [C# extension](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csharp).
- The [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).

Or

- [Visual Studio 2022](https://visualstudio.microsoft.com/downloads/?utm_medium=microsoft&utm_source=learn.microsoft.com&utm_campaign=inline+link&utm_content=download+vs2022) with the _.NET desktop development_ workload installed.

## Create the app

- Create a .NET 8 console app project named "ConsoleApp".

- Create a folder named _ConsoleApp_ for the project, and then open a command prompt in the new folder.

Run the following command:

```console
dotnet new console --framework net8.0
```

## Install the Xping.Sdk package

Run the following command:

```console
dotnet add package Xping.Sdk --prerelease
```

The `--prerelease` option is necessary because the library is still in beta.

Additionally install `CommandLine` package which we will use in our _ConsoleApp_ to parse and handle command line arguments.

```console
dotnet add package System.CommandLine --prerelease
```

For more information on how to use `CommandLine` please follow [Command Line Tutorial](https://learn.microsoft.com/en-us/dotnet/standard/commandline/get-started-tutorial) 

#### Replace the content of the Program.cs with the following code

```csharp
class Program : XpingAssertions
{
    const int EXIT_SUCCESS = 0;
    const int EXIT_FAILURE = 1;

    const int MAX_SIZE_IN_BYTES = 153600; // 150kB

    static async Task<int> Main(string[] args)
    {
        IHost host = CreateHostBuilder(args).Build();

        var urlOption = new Option<Uri?>(
            name: "--url",
            description: "A URL address of the page being validated.")
        { IsRequired = true };

        var command = new RootCommand("Sample application for Xping SDK");
        command.AddOption(urlOption);
        command.SetHandler(async (InvocationContext context) =>
        {
            Uri url = context.ParseResult.GetValueForOption(urlOption)!;
            var testAgent = host.Services.GetRequiredService<TestAgent>();

            testAgent
                .UseDnsLookup()
                .UseIPAddressAccessibilityCheck()
                .UseHttpClient()
                .UseHttpValidation(response =>
                {
                    Expect(response)
                        .ToHaveSuccessStatusCode()
                        .ToHaveHttpHeader(HeaderNames.Server)
                            .WithValue("Google", new() { Exact = false });
                })
                .UseHtmlValidation(html =>
                {
                    html.HasMaxDocumentSize(MAX_SIZE_IN_BYTES);
                });

            await using var session = await testAgent.RunAsync(url);

            context.Console.WriteLine("\nSummary:");
            context.Console.WriteLine($"{session}");
            context.ExitCode = session.IsValid ? EXIT_SUCCESS : EXIT_FAILURE;
        });

        return await command.InvokeAsync(args);
    }

    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((services) =>
            {
                services.AddSingleton<IProgress<TestStep>, Progress>()
                        .AddHttpClientFactory()
                        .AddTestAgent(agent =>
                        {
                            agent.UploadToken = "--- Your Dashboard Upload Token ---"; // optional
                        });
            })
            .ConfigureLogging(logging =>
            {
                logging.AddFilter(typeof(HttpClient).FullName, LogLevel.Warning);
            });
}
```

#### Add reporting mechanism

Create a new class `Progress.cs` and replace its content with following:

```csharp
public sealed class Progress(ILogger<Program> logger) : IProgress<TestStep>
{
    private static readonly Action<ILogger, string, Exception?> LogSuccessMessage = LoggerMessage.Define<string>(
        logLevel: LogLevel.Information, eventId: 1, formatString: "{Value}");
    private static readonly Action<ILogger, string, Exception?> LogErrorMessage = LoggerMessage.Define<string>(
        logLevel: LogLevel.Error, eventId: 2, formatString: "{Value}");

    private readonly ILogger<Program> _logger = logger;

    public void Report(TestStep value)
    {
        ArgumentNullException.ThrowIfNull(value, nameof(value));

        switch (value.Result)
        {
            case TestStepResult.Succeeded:
                LogSuccessMessage(_logger, value.ToString(), null);
                break;
            case TestStepResult.Failed:
                LogErrorMessage(_logger, value.ToString(), null);
                break;
        }
    }
}
```

The `IProgress<TestStep>` interface is implemented by this class, which is called on every test step performed by @Xping.Sdk.Core.TestAgent during its testing operation. This allows to monitor the progress of the test execution.

The preceding code we added earlier in `Program.cs` does following:

- Creates a host with preconfigured defaults, and then adds the `Progress` class and `IHttpClientFactory` to the service collection. 

> [!NOTE]
> To test with a headless browser, consider using `services.AddBrowserClientFactory()` instead.

```csharp
static IHostBuilder CreateHostBuilder(string[] args) =>
    Host.CreateDefaultBuilder(args)
        .ConfigureServices((services) =>
        {
            services.AddSingleton<IProgress<TestStep>, Progress>()
                    .AddHttpClientFactory()
            (...)
        })
```

> [!NOTE]
> For more information on how to use dependency injection in .NET please follow this [Dependency Injection Tutorial](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection-usage).

- Adds a @Xping.Sdk.Core.TestAgent service to the service collection.

```csharp
services.AddTestAgent(agent =>
{
    agent.UploadToken = "--- Your Dashboard Upload Token ---"; // optional
});
```

- Creates an option named `--url` of type `Uri` and assigns it to the root command:

```csharp
var urlOption = new Option<Uri?>(
    name: "--url",
    description: "A URL address of the page being validated.")
{ IsRequired = true };

var command = new RootCommand("Sample application for Xping SDK");
command.AddOption(urlOption);
```

- Specifies the handler method that will be called when the root command is invoked and parses the `url` option:

```csharp
command.SetHandler(async (InvocationContext context) =>
{
    Uri url = context.ParseResult.GetValueForOption(urlOption)!;
    
    (...)
});

return await command.InvokeAsync(args);
```

- The handler method retrieves the @Xping.Sdk.Core.TestAgent service, configures its testing pipeline, and runs test operations with default test settings against the `url` value:

```csharp

command.SetHandler(async (InvocationContext context) =>
{
    (...)

    var testAgent = host.Services.GetRequiredService<TestAgent>();

    testAgent
        .UseDnsLookup()
        .UseIPAddressAccessibilityCheck()
        .UseHttpClient()
        .UseHttpValidation(response =>
        {
            Expect(response)
                .ToHaveSuccessStatusCode()
                .ToHaveHttpHeader(HeaderNames.Server)
                    .WithValue("Google", new() { Exact = false });
        })
        .UseHtmlValidation(html =>
        {
            html.HasMaxDocumentSize(MAX_SIZE_IN_BYTES);
        });

    await using var session = await testAgent.RunAsync(url);
});
```

- Prints out summary and specifies exit code depending on the test results:

```csharp
command.SetHandler(async (InvocationContext context) =>
{
    (...)

    context.Console.WriteLine("\nSummary:");
    context.Console.WriteLine($"{session}");
    context.ExitCode = session.IsValid ? EXIT_SUCCESS : EXIT_FAILURE;
});

return await command.InvokeAsync(args);
```

## Test the app

Run the `dotnet build` command, and then open a command prompt in the `ConsoleApp/bin/Debug/net8.0` folder to run the executable:

```console
dotnet build
cd bin/Debug/net8.0
ConsoleApp --url https://demoblaze.com
```

Upon running the application, it performs availability tests on the URL specified by the `--url` option and prints the results:

```console
ConsoleApp.exe --url http://demoblaze.com

info: ConsoleAppTesting.Program[1]
      9/22/2024 9:11:04 PM (29 ms) [ActionStep]: DNS lookup succeeded.
info: ConsoleAppTesting.Program[1]
      9/22/2024 9:11:04 PM (18 ms) [ActionStep]: IPAddress accessibility check succeeded.
info: ConsoleAppTesting.Program[1]
      9/22/2024 9:11:04 PM (343 ms) [ActionStep]: HttpClientRequestSender succeeded.
info: ConsoleAppTesting.Program[1]
      9/22/2024 9:11:04 PM (4 ms) [ValidateStep]: HttpResponseValidator:EnsureSuccessStatusCode succeeded.
info: ConsoleAppTesting.Program[1]
      9/22/2024 9:11:04 PM (2 ms) [ValidateStep]: HttpResponseValidator:HttpHeader succeeded.
info: ConsoleAppTesting.Program[1]
      9/22/2024 9:11:04 PM (70 ms) [ValidateStep]: HtmlContentValidator:HasMaxDocumentSize succeeded.

Summary:
9/22/2024 9:11:04 PM (465 ms) Test session completed for https://demoblaze.com/.
Total steps: 6, Success: 6, Failures: 0
```

Congratulations! You have successfully completed this tutorial on how to use the Xping SDK. You have learned how to create a .NET console application that utilizes the `Xping SDK` library to automate web application testing. You have also learned how to create a basic test agent, add a reporting service, and build a validation pipeline that contains multiple test components.

For a complete implementation of this tutorial, please refer to our [sample](https://github.com/Xping/Xping-sdk/tree/main/samples) folder.