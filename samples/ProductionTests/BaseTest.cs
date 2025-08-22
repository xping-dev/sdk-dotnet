/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using Xping.Sdk;
using Xping.Sdk.Core;

namespace ProductionTests;

public abstract class BaseTest(TestAgent testAgent) : XpingAssertions
{
    private readonly Uri _baseUri = new("https://demoblaze.com");

    public required TestAgent TestAgent { get; init; } = testAgent;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        // Upload Token - Associates test sessions with your specific project on the Xping services.
        // This token links your test results to the correct project for monitoring and analysis.
        // Get your project's upload token from the Xping Dashboard at https://app.xping.io
        TestAgent.UploadToken = "--- Your Dashboard Upload Token ---";
        // API Key for authentication with Xping services.
        // Can be set explicitly or read from XPING_API_KEY environment variable.
        // Get your project's API key from the Xping Dashboard at https://app.xping.io/
        TestAgent.ApiKey = "--- Your Dashboard API Key ---";
        TestAgent.UploadFailed += (sender, args) =>
        {
            Console.WriteLine($"{args.UploadResult.Message}");
        };
        TestAgent.UploadSucceeded += (sender, args) =>
        {
            Console.WriteLine($"Upload succeeded at {args.UploadedAt}");
        };
    }

    protected async Task RunAsync(string? relativeAddress = null)
    {
        var address = relativeAddress != null ? new Uri(_baseUri, relativeAddress) : _baseUri;
        await using var session = await TestAgent.RunAsync(address);

        Assert.That(session.IsValid, Is.True, session.Failures.FirstOrDefault()?.ErrorMessage);
    }
}