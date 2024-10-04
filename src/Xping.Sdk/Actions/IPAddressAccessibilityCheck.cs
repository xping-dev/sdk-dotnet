using System.Collections.ObjectModel;
using System.Net;
using System.Net.NetworkInformation;
using Xping.Sdk.Actions.Configurations;
using Xping.Sdk.Core.Common;
using Xping.Sdk.Core.Components;
using Xping.Sdk.Core.Session;

namespace Xping.Sdk.Actions;

/// <summary>
/// The IPAddressAccessibilityCheck class is a concrete implementation of the <see cref="TestComponent"/> class that 
/// is used to check the accessibility of an IP address. It uses the mechanisms provided by the operating system to 
/// check the accessibility of an IP address.
/// </summary>
/// <remarks>
/// <note>
/// The IPAddressAccessibilityCheck component requires the DnsLookup component to be registered before it in the 
/// pipeline, because it depends on the DNS resolution results.
/// </note>
/// The IPAddressAccessibilityCheck component uses the <see cref="Ping"/> class to send an Internet Control Message 
/// Protocol (ICMP) echo request to the IP address and receive an echo reply. The Ping class can handle both synchronous 
/// and asynchronous operations, and can also provide information such as the round-trip time, the time-to-live, and the 
/// buffer size of the ICMP packets. The IPAddressAccessibilityCheck component can use these information to determine 
/// the availability and performance of the IP address.
/// </remarks>
public sealed class IPAddressAccessibilityCheck : TestComponent
{
    private readonly PingConfiguration _configuration;

    /// <summary>
    /// The name of the test component that represents a IPAddressAccessibilityCheck of tests.
    /// </summary>
    /// <remarks>
    /// This constant is used to register the IPAddressAccessibilityCheck class in the test framework.
    /// </remarks>
    public const string StepName = "IPAddress accessibility check";

    /// <summary>
    /// Initializes new instance of th
    /// </summary>
    /// <param name="configuration">The ping operation configuration.</param>
    public IPAddressAccessibilityCheck(PingConfiguration configuration) :
        base(name: StepName, type: TestStepType.ActionStep)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// This method performs the test step operation asynchronously.
    /// </summary>
    /// <param name="url">A Uri object that represents the URL of the page being validated.</param>
    /// <param name="settings">A <see cref="TestSettings"/> object that contains the settings for the test.</param>
    /// <param name="context">A <see cref="TestContext"/> object that represents the test context.</param>
    /// <param name="serviceProvider">An instance object of a mechanism for retrieving a service object.</param>
    /// <param name="cancellationToken">An optional CancellationToken object that can be used to cancel
    /// this operation.</param>
    /// <returns><see cref="TestStep"/> object.</returns>
    public override async Task HandleAsync(
        Uri url,
        TestSettings settings,
        TestContext context,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url, nameof(url));
        ArgumentNullException.ThrowIfNull(settings, nameof(settings));
        ArgumentNullException.ThrowIfNull(context, nameof(context));

        ReadOnlyCollection<IPAddress>? addresses = GetIPAddresses(context.SessionBuilder.Steps);
        using var pingSender = new Ping();
        bool completed = false;
        int addressIndex = 0;
        TestStep testStep = null!;

        try
        {
            if (addresses == null || addresses.Count == 0)
            {
                testStep = context.SessionBuilder.Build(Errors.InsufficientData(component: this));
            }
            else
            {
                do
                {
                    IPAddress address = addresses[addressIndex++];
                    PingReply reply = await pingSender.SendPingAsync(
                        address: address,
                        timeout: _configuration.Timeout,
                        // On Linux, non-privileged processes can't send a custom payload. Please leave this empty.
                        buffer: [],
                        options: GetOptions(),
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    if (cancellationToken.IsCancellationRequested)
                    {
                        testStep = context.SessionBuilder.Build(Errors.TestComponentTimeout(this, settings.Timeout));
                        context.Progress?.Report(testStep);
                        return;
                    }

                    if (reply.Status == IPStatus.Success)
                    {
                        testStep = context.SessionBuilder
                            .Build(PropertyBagKeys.IPAddress, new PropertyBagValue<string>(address.ToString()))
                            .Build();
                        completed = true;
                    }
                    else
                    {
                        testStep = context.SessionBuilder.Build(Errors.PingRequestFailed);
                        completed = false;
                    }

                    context.Progress?.Report(testStep);

                    // This step implements a fail-fast mechanism that terminates the iteration as soon as the first
                    // IP address passes. If an error occurs, the iteration continues with the next IP address.
                } while (completed != true && addressIndex < addresses.Count);
            }
        }
        catch (Exception exception)
        {
            testStep = context.SessionBuilder.Build(exception);
            context.Progress?.Report(testStep);
        }
    }

    private PingOptions GetOptions() => new()
    {
        Ttl = _configuration.Ttl
    };

    private static ReadOnlyCollection<IPAddress>? GetIPAddresses(IReadOnlyCollection<TestStep> steps)
    {
        PropertyBagValue<string[]>? bag = null;

        if (steps.Any(
            step => step.PropertyBag != null &&
            step.PropertyBag.TryGetProperty(key: PropertyBagKeys.DnsResolvedIPAddresses, value: out bag)) &&
            bag != null)
        {
            var result = bag.Value.Select(IPAddress.Parse).ToList();
            return result.AsReadOnly();
        }

        return null;
    }
}
