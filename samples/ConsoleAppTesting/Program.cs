/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Xping.Sdk;
using Xping.Sdk.Core;
using Xping.Sdk.Core.Components;
using Xping.Sdk.Core.DependencyInjection;
using Xping.Sdk.Core.Session;
using Xping.Sdk.Extensions;

namespace ConsoleAppTesting;

internal sealed class Program : XpingAssertions
{
    const int EXIT_SUCCESS = 0;
    const int EXIT_FAILURE = 1;

    private const int MaxHtmlSizeAllowed = 150 * 1024; // 150 KB in bytes
    private const int MaxResponseTimeMs = 3000; // 3 seconds in milliseconds

    static async Task<int> Main(string[] args)
    {
        IHost host = CreateHostBuilder(args).Build();

        var urlOption = new Option<Uri>(
            name: "--url",
            description: "A URL address of the page being validated.")
        {
            IsRequired = true,
        };

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
                        .ToHaveResponseTimeLessThan(TimeSpan.FromMilliseconds(MaxResponseTimeMs))
                        .ToHaveHeaderWithValue(HeaderNames.Server, value: "Google");
                })
                .UseHtmlValidation(html =>
                {
                    Expect(html)
                        .ToHaveMaxDocumentSize(MaxHtmlSizeAllowed)
                        .ToHaveMetaTag(new("property", "og:image"), "https://a.wpimg.pl/a/f/png/37220/wpogimage.png");
                });

            await using var session = await testAgent.RunAsync(url, new TestSettings{ ContinueOnFailure = true })
                .ConfigureAwait(false);

            context.Console.WriteLine("\nSummary:");
            context.Console.WriteLine($"{session}");

            if (session.IsUploaded)
            {
                context.Console.WriteLine($"Test session uploaded successfully at {session.UploadedAt}.");
            }

            context.ExitCode = session.IsValid ? EXIT_SUCCESS : EXIT_FAILURE;
        });

        return await command.InvokeAsync(args).ConfigureAwait(false);
    }

    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((services) =>
            {
                services.AddSingleton<IProgress<TestStep>, Progress>()
                        .AddHttpClientFactory()
                        .AddTestAgent(agent =>
                        {
                            // Token for uploading test sessions
                            agent.UploadToken = "f3bdf647-3bff-40c1-a69a-e86b82f92fa1";
                            // API Key for authentication with Xping services
                            // Can be set explicitly or read from XPING_API_KEY environment variable
                            agent.ApiKey = "xpg_cDgznhrUQ4IuybrWH7hztw";
                            agent.UploadFailed += (sender, args) =>
                            {
                                Console.WriteLine($"Upload failed: {args.UploadResult.Message}");
                            };
                        });
            })
            .ConfigureLogging(logging =>
            {
                logging.AddFilter(typeof(HttpClient).FullName, LogLevel.Warning);
            });
}
