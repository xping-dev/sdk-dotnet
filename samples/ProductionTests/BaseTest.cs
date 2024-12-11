using Xping.Sdk;
using Xping.Sdk.Core;

namespace ProductionTests;

public abstract class BaseTest(TestAgent testAgent) : XpingAssertions
{
    private readonly Uri _baseUri  = new("https://demoblaze.com");

    public required TestAgent TestAgent { get; init; } = testAgent;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        TestAgent.UploadToken = "--- Your Dashboard Upload Token ---";
    }
    
    protected async Task RunAsync(string? relativeAddress = null)
    {
        var address = relativeAddress != null ? new Uri(_baseUri, relativeAddress) : _baseUri;
        await using var session = await TestAgent.RunAsync(address);
            
        Assert.That(session.IsValid, Is.True, session.Failures.FirstOrDefault()?.ErrorMessage);
    }
}