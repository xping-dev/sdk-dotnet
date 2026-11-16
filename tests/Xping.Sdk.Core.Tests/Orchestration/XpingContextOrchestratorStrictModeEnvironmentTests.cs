/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Services.Environment;
using Xping.Sdk.Core.Services.Upload;
using Xping.Sdk.Core.Tests.Helpers;

namespace Xping.Sdk.Core.Tests.Orchestration;

/// <summary>
/// How <see cref="XpingContextOrchestrator"/> reads <c>XPING_STRICTMODE</c> when its options never
/// resolve. Kept apart from <see cref="XpingContextOrchestratorTests"/> because the orchestrator reads
/// the process environment itself, so these tests set the real variable.
/// </summary>
[Collection("Sequential")]
public sealed class XpingContextOrchestratorStrictModeEnvironmentTests : IDisposable
{
    private const string VariableName = "XPING_STRICTMODE";

    private readonly string? _ambientValue = Environment.GetEnvironmentVariable(VariableName);

    public void Dispose() => Environment.SetEnvironmentVariable(VariableName, _ambientValue);

    private sealed class TestOrchestrator(IHost host) : XpingContextOrchestrator(host)
    {
        public bool Collecting => IsCollecting;

        protected override string TestFramework => "Harness";
    }

    [Fact]
    public async Task XpingStrictModeFalseWinsOverTheXpingSectionWhenTheOptionsFailToResolve()
    {
        Environment.SetEnvironmentVariable(VariableName, "false");
        using var store = new ScratchStore();
        var host = ServiceHelper.BuildRegisteredOrchestratorHost(
            ServiceHelper.CloudConfiguration(store.Path, strictMode: false),
            new Mock<IXpingUploader>(),
            new Mock<IEnvironmentDetector>(),
            appConfiguration: new Dictionary<string, string?> { ["Xping:StrictMode"] = "true" },
            configureServices: services => services.Configure<XpingConfiguration>(
                _ => throw new InvalidOperationException("options broke")));

        var orchestrator = new TestOrchestrator(host);

        Assert.False(orchestrator.Collecting);
        await orchestrator.DisposeAsync();
    }
}
