/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Reflection;
using Microsoft.Extensions.Hosting;
using Moq;
using Xping.Sdk.Core;
using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Environments;
using Xping.Sdk.Core.Services.Environment;
using Xping.Sdk.Core.Services.LocalStore;
using Xping.Sdk.Core.Services.Upload;
using Xping.Sdk.Core.Tests.Helpers;

namespace Xping.Sdk.Core.Tests.Orchestration;

/// <summary>
/// Guards the copy the orchestrator makes of the detected environment before writing it locally.
/// </summary>
/// <remarks>
/// <para>
/// The local copy exists to stamp the recording mode onto the session without that stamp reaching
/// the upload, and it is written property by property. That is a shape which fails silently: a
/// property added to <see cref="EnvironmentInfo"/> and wired all the way through the detector,
/// the builder and the serializer still arrives empty in <c>.xping/</c> if one line here is
/// missed, and every unit test either side of it passes.
/// </para>
/// <para>
/// It has failed that way once already, on the UTC offset. So this asserts completeness by
/// reflection rather than naming fields: a property added tomorrow is covered without anyone
/// remembering to come back here.
/// </para>
/// </remarks>
[Collection("Sequential")]
public sealed class RecordingModeCopyTests
{
    private sealed class Harness(IHost host) : XpingContextOrchestrator(host);

    [Fact]
    public async Task TheLocalCopyPreservesEveryPropertyOfTheDetectedEnvironment()
    {
        EnvironmentInfo detected = Populated();
        EnvironmentInfo copied = await LocalCopyOf(detected);

        foreach (PropertyInfo property in typeof(EnvironmentInfo).GetProperties())
        {
            // The one property the copy is entitled to change: stamping the recording mode is the
            // reason it exists. Its other entries still have to survive, which is checked below.
            if (property.Name == nameof(EnvironmentInfo.CustomProperties))
                continue;

            Assert.Equal(property.GetValue(detected), property.GetValue(copied));
        }
    }

    [Fact]
    public async Task TheLocalCopyKeepsTheDetectedCustomPropertiesAndAddsTheMode()
    {
        EnvironmentInfo copied = await LocalCopyOf(Populated());

        Assert.Equal("main", copied.CustomProperties["Git.Branch"]);
        Assert.True(copied.CustomProperties.ContainsKey(LocalSessionProperties.Mode));
    }

    [Fact]
    public async Task TheLocalCopyLeavesAnUnrecordedTimeZoneUnrecorded()
    {
        // Null must not become a zero offset on the way to disk. Local analysis excludes the first
        // and would read the second as a machine genuinely running on UTC.
        EnvironmentInfo copied = await LocalCopyOf(new EnvironmentInfoBuilder().Build());

        Assert.Null(copied.UtcOffset);
        Assert.Null(copied.TimeZoneId);
    }

    /// <summary>
    /// Builds an environment with every property set to something distinguishable from its default.
    /// </summary>
    /// <returns>The environment.</returns>
    /// <remarks>
    /// Defaults would let a dropped property pass: a copy that never assigned
    /// <see cref="EnvironmentInfo.TimeZoneId"/> matches a source whose zone was null too.
    /// </remarks>
    private static EnvironmentInfo Populated() =>
        new EnvironmentInfoBuilder()
            .WithMachineName("build-agent-01")
            .WithOperatingSystem("Ubuntu 22.04")
            .WithRuntimeVersion(".NET 10.0.0")
            .WithFramework(".NET")
            .WithEnvironmentName("CI")
            .WithIsCIEnvironment(true)
            .WithLocalTimeZone(TimeSpan.FromHours(-5.5), "Some/Zone")
            .AddCustomProperties(new Dictionary<string, string> { ["Git.Branch"] = "main" })
            .Build();

    /// <summary>
    /// Runs one environment through the orchestrator's local-copy step.
    /// </summary>
    /// <param name="detected">What the environment detector produced.</param>
    /// <returns>What would be written to the local store.</returns>
    /// <remarks>
    /// Reached by reflection because the step is private and should stay so — it is an
    /// implementation detail of writing a session, not something a caller has any business
    /// invoking. Widening it to be testable would be a worse trade than this lookup.
    /// </remarks>
    private static async Task<EnvironmentInfo> LocalCopyOf(EnvironmentInfo detected)
    {
        var uploader = new Mock<IXpingUploader>();
        var detector = new Mock<IEnvironmentDetector>();
        ServiceHelper.SetupDefaultMocks(uploader, detector);

        using IHost host = ServiceHelper.BuildOrchestratorHost(uploader, detector);
        var orchestrator = new Harness(host);

        try
        {
            MethodInfo copy = typeof(XpingContextOrchestrator).GetMethod(
                "WithRecordingMode", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException(
                    "XpingContextOrchestrator.WithRecordingMode is gone. If the local copy was " +
                    "replaced, this guard needs to point at whatever replaced it — not to be deleted.");

            return (EnvironmentInfo)copy.Invoke(orchestrator, [detected])!;
        }
        finally
        {
            await orchestrator.DisposeAsync().ConfigureAwait(false);
        }
    }
}
