/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Xping.Sdk.Core.Configuration;

namespace Xping.Sdk.Core.Tests.Configuration;

// Sets the real XPING_CAPTURESTACKTRACES, because the resolver reads the process environment itself.
[Collection("Sequential")]
public sealed class CaptureStackTraceConfigurationResolverTests : IDisposable
{
    private const string VariableName = "XPING_CAPTURESTACKTRACES";

    private readonly string? _ambientValue = Environment.GetEnvironmentVariable(VariableName);

    public CaptureStackTraceConfigurationResolverTests() =>
        Environment.SetEnvironmentVariable(VariableName, null);

    public void Dispose() => Environment.SetEnvironmentVariable(VariableName, _ambientValue);

    [Fact]
    public void TheEnvironmentVariableWinsOverTheXpingSectionWhenOptionsFailValidation()
    {
        Environment.SetEnvironmentVariable(VariableName, "false");
        IServiceProvider services = BuildServicesWithInvalidOptions(sectionValue: "true");

        Assert.False(CaptureStackTraceConfigurationResolver.ResolveCaptureStackTraces(services));
    }

    [Fact]
    public void TheXpingSectionIsUsedWhenTheEnvironmentVariableIsUnset()
    {
        IServiceProvider services = BuildServicesWithInvalidOptions(sectionValue: "false");

        Assert.False(CaptureStackTraceConfigurationResolver.ResolveCaptureStackTraces(services));
    }

    [Fact]
    public void StackTracesAreCapturedWhenNothingSetsThem()
    {
        IServiceProvider services = BuildServicesWithInvalidOptions(sectionValue: null);

        Assert.True(CaptureStackTraceConfigurationResolver.ResolveCaptureStackTraces(services));
    }

    private static ServiceProvider BuildServicesWithInvalidOptions(string? sectionValue)
    {
        var options = new Mock<IOptions<XpingConfiguration>>();
        options
            .Setup(o => o.Value)
            .Throws(new OptionsValidationException(Options.DefaultName, typeof(XpingConfiguration), ["invalid"]));

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Xping:CaptureStackTraces"] = sectionValue })
            .Build();

        return new ServiceCollection()
            .AddSingleton(options.Object)
            .AddSingleton(configuration)
            .BuildServiceProvider();
    }
}
