/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.DependencyInjection;
using Xping.Cli.Commands;
using Xping.Cli.Hosting;

namespace Xping.Cli.Tests.Commands;

/// <summary>
/// <c>export XPING_NO_BANNER=1</c> reaches the throttle the CLI actually builds.
/// </summary>
/// <remarks>
/// Every other throttle test injects its environment, so only this one proves the production
/// wiring reads the process. Sequential because it sets the real variable.
/// </remarks>
[Collection("Sequential")]
public sealed class CtaThrottleProcessEnvironmentTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xping-cta-process-tests", Guid.NewGuid().ToString("N"));

    public CtaThrottleProcessEnvironmentTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(CtaThrottle.SuppressBannerVariable, null);

        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void TheProcessVariableSuppressesTheInvitation()
    {
        using ServiceProvider services = new ServiceCollection()
            .AddLogging()
            .AddXpingCliServices(TextWriter.Null, TextWriter.Null, TextReader.Null, Terminals.All(false))
            .BuildServiceProvider();

        Environment.SetEnvironmentVariable(CtaThrottle.SuppressBannerVariable, "1");

        Assert.False(services.GetRequiredService<CtaThrottle>().ShouldShow(_root, hasFindings: true, isConnected: false));
    }
}
