/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Net;
using Xping.Sdk.Core.Common;
using Xping.Sdk.Core.Components;
using Xping.Sdk.Core.Session;

namespace Xping.Sdk.Actions;

/// <summary>
/// The DnsLookup class is a concrete implementation of the <see cref="TestComponent"/> class that is used to perform 
/// a DNS lookup. It uses the mechanisms provided by the operating system to perform DNS lookups. 
/// </summary>
/// <remarks>
/// <note>
/// This component operates without any dependency on other components. Please also note, the DNS lookup method may use 
/// cached data from the local machine if the DNS information for the host has been cached previously.
/// </note>
/// The DnsLookup mechanism works by sending a query to a DNS server and receiving a response that contains the IP 
/// address of the requested domain name. The query can be either recursive or iterative, depending on the configuration 
/// of the DNS server. A recursive query instructs the DNS server to return the final answer or an error, while an 
/// iterative query instructs the DNS server to return the best answer it has or a referral to another DNS server. 
/// The DnsLookup component uses the mechanisms provided by the operating system to perform DNS lookups. This means that 
/// it can handle both iterative and recursive queries, depending on the configuration of the DNS server. The DNS lookup
/// method can also follow the referrals until it reaches the authoritative DNS server that has the definitive answer. 
/// However, the component does not expose any option to specify the query type or the DNS server to use.
/// </remarks>
public sealed class DnsLookup : TestComponent
{
    /// <summary>
    /// The name of the test component that represents a DnsLookup test operation.
    /// </summary>
    /// <remarks>
    /// This constant is used to register the DnsLookup class in the test framework.
    /// </remarks>
    public const string ComponentName = "DNS lookup";

    /// <summary>
    /// Initializes a new instance of the DnsLookup class.
    /// </summary>
    public DnsLookup() : base(name: ComponentName, type: TestStepType.ActionStep)
    { }

    /// <summary>
    /// This method performs the test step operation asynchronously.
    /// </summary>
    /// <param name="url">A Uri object that represents the URL of the page being validated.</param>
    /// <param name="settings">A <see cref="TestSettings"/> object that contains the settings for the test.</param>
    /// <param name="context">A <see cref="TestContext"/> object that represents the test context.</param>
    /// <param name="serviceProvider">An instance object of a mechanism for retrieving a service object.</param>
    /// <param name="cancellationToken">
    /// An optional CancellationToken object that can be used to cancel the this operation.
    /// </param>
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

        TestStep testStep = null!;
        try
        {
            IPHostEntry resolved = await Dns.GetHostEntryAsync(url.Host, cancellationToken).ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
            {
                testStep = context.SessionBuilder.Build(Errors.TestComponentTimeout(this, settings.Timeout));
                return;
            }

            if (resolved != null && resolved.AddressList != null && resolved.AddressList.Length != 0)
            {
                testStep = context.SessionBuilder
                    .Build(
                        key: PropertyBagKeys.DnsResolvedIPAddresses,
                        value: new PropertyBagValue<string[]>(
                            Array.ConvertAll(resolved.AddressList, addr => addr.ToString())))
                    .Build();
            }
            else
            {
                testStep = context.SessionBuilder.Build(Errors.DnsLookupFailed);
            }
        }
        catch (Exception exception)
        {
            testStep = context.SessionBuilder.Build(exception);
        }
        finally
        {
            context.Progress?.Report(testStep);
        }
    }
}
