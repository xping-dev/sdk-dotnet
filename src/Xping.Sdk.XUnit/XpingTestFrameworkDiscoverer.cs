/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xunit;
using Xunit.Abstractions;

namespace Xping.Sdk.XUnit;

/// <summary>
/// Wraps xUnit's discoverer to count the test cases a whole-assembly discovery finds.
/// </summary>
/// <remarks>
/// Discovery itself is untouched: every message is forwarded to the runner as it arrives. Only
/// <see cref="Find(bool, IMessageSink, ITestFrameworkDiscoveryOptions)"/> is counted, because it is
/// the search of the whole assembly; the overload searching one class passes straight through.
/// </remarks>
internal sealed class XpingTestFrameworkDiscoverer(ITestFrameworkDiscoverer inner, TestCaseCensus census)
    : LongLivedMarshalByRefObject, ITestFrameworkDiscoverer
{
    /// <inheritdoc/>
    public string TargetFramework => inner.TargetFramework;

    /// <inheritdoc/>
    public string TestFrameworkDisplayName => inner.TestFrameworkDisplayName;

    /// <inheritdoc/>
    public void Find(
        bool includeSourceInformation,
        IMessageSink discoveryMessageSink,
        ITestFrameworkDiscoveryOptions discoveryOptions) =>
        inner.Find(includeSourceInformation, new CountingSink(discoveryMessageSink, census), discoveryOptions);

    /// <inheritdoc/>
    public void Find(
        string typeName,
        bool includeSourceInformation,
        IMessageSink discoveryMessageSink,
        ITestFrameworkDiscoveryOptions discoveryOptions) =>
        inner.Find(typeName, includeSourceInformation, discoveryMessageSink, discoveryOptions);

    /// <inheritdoc/>
    public string Serialize(ITestCase testCase) => inner.Serialize(testCase);

    /// <inheritdoc/>
    public void Dispose() => inner.Dispose();

    /// <summary>
    /// Counts each discovered test case on its way to the runner.
    /// </summary>
    private sealed class CountingSink(IMessageSink inner, TestCaseCensus census)
        : LongLivedMarshalByRefObject, IMessageSink
    {
        public bool OnMessage(IMessageSinkMessage message)
        {
            switch (message)
            {
                case ITestCaseDiscoveryMessage discovered:
                    census.AddDiscovered(discovered.TestCase.UniqueID);
                    break;
                case IDiscoveryCompleteMessage:
                    census.CompleteDiscovery();
                    break;
            }

            return inner.OnMessage(message);
        }
    }
}
