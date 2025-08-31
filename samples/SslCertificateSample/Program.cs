/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xping.Sdk.Core;
using Xping.Sdk.Core.DependencyInjection;
using Xping.Sdk.Extensions;
using Xping.Sdk.Validations.TextUtils;

namespace SslCertificateSample;

/// <summary>
/// Demonstrates SSL certificate capture and validation using the Xping SDK.
/// This class inherits from XpingAssertions to access the Expect() method for fluent assertions.
/// </summary>
internal sealed class Program : Xping.Sdk.XpingAssertions
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("Xping SDK - SSL Certificate Validation Sample");
        Console.WriteLine("=" + new string('=', 50));

        var url = new Uri("https://www.google.com");

        Console.WriteLine($"Testing SSL certificate for: {url}");
        Console.WriteLine("=" + new string('=', 50));

        // Create a host with dependency injection
        IHost host = CreateHostBuilder(args).Build();

        try
        {
            var testAgent = host.Services.GetRequiredService<TestAgent>();
            // Create a test agent with SSL certificate capture and validation
            await testAgent
                .UseSslCertificateCapture()          // Capture SSL certificate data
                .UseHttpClient()                     // Use HttpClient for requests
                .UseSslCertificateValidation(ssl =>  // Validate the captured certificate
                {
                    Console.WriteLine("Validating SSL certificate...");

                    // Use Expect() to get fluent assertions for SSL certificate
                    // Validate that the certificate is valid
                    Expect(ssl).ToBeValid();
                    Console.WriteLine("✓ Certificate is valid");

                    // Validate that the certificate subject contains Google
                    Expect(ssl).ToHaveSubjectContaining("Google", new TextOptions(MatchCase: false));
                    Console.WriteLine("✓ Subject contains 'Google'");

                    // Validate that the certificate expires after today
                    Expect(ssl).ToExpireAfter(DateTime.UtcNow);
                    Console.WriteLine("✓ Certificate is not expired");

                    // Validate that the SAN contains the domain we're testing
                    Expect(ssl).ToHaveSanContaining("google.com", new TextOptions(MatchCase: false));
                    Console.WriteLine("✓ SAN contains 'google.com'");

                    // Additional validations
                    Expect(ssl).ToHaveKeySize(2048); // RSA 2048-bit key
                    Console.WriteLine("✓ Certificate uses 2048-bit key");

                    // Validate signature algorithm is secure
                    Expect(ssl).ToHaveSignatureAlgorithm("sha256RSA");
                    Console.WriteLine("✓ Certificate uses SHA-256 signature algorithm");
                })
                .RunAsync(url).ConfigureAwait(false);

            Console.WriteLine();
            Console.WriteLine("SSL certificate validation completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SSL certificate validation failed: {ex.Message}");
            Environment.Exit(1);
        }
    }
    
    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((services) =>
            {
                services.AddHttpClientFactory()
                        .AddTestAgent(agent =>
                        {
                            // Upload Token - Associates test sessions with your specific project on the Xping services.
                            // This token links your test results to the correct project for monitoring and analysis.
                            // Get your project's upload token from the Xping Dashboard at https://app.xping.io
                            agent.UploadToken = "--- Your Dashboard Upload Token ---";
                            // API Key for authentication with Xping services.
                            // Can be set explicitly or read from XPING_API_KEY environment variable.
                            // Get your project's API key from the Xping Dashboard at https://app.xping.io/
                            agent.ApiKey = "--- Your API Key ---";
                            agent.UploadFailed += (sender, args) =>
                            {
                                WriteErrorMessage(args.UploadResult.Message);
                            };
                            agent.UploadSucceeded += (sender, args) =>
                            {
                                WriteSuccessMessage(
                                    $"Upload succeeded: \n" +
                                    $"Session uploaded at {args.UploadedAt} with token {args.UploadToken}");
                            };
                        });
            })
            .ConfigureLogging(logging =>
            {
                logging.AddFilter(typeof(HttpClient).FullName, LogLevel.Warning);
            });

    private static void WriteErrorMessage(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    private static void WriteSuccessMessage(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(message);
        Console.ResetColor();
    }
}
