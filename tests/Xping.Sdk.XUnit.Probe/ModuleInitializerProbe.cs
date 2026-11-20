/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Runtime.CompilerServices;
using Xping.Sdk.Core.Configuration;
using Xunit;

namespace Xping.Sdk.XUnit.Probe;

/// <summary>
/// Configures the SDK from a module initializer, as the xUnit quick start shows. The test that runs this
/// assembly names the store in <see cref="StoreKey"/>; anywhere else the initializer does nothing.
/// The test must not name a type of this assembly in code: the runtime runs the initializer as soon as
/// a method that does is compiled.
/// </summary>
public static class ModuleInitializerProbe
{
    /// <summary>The <see cref="AppContext"/> key that holds the local store path to configure.</summary>
    public const string StoreKey = "Xping.Sdk.XUnit.Probe.Store";

    // CA2255: a module initializer in a library is what this probe exists to exercise.
#pragma warning disable CA2255
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void Initialize()
    {
        if (AppContext.GetData(StoreKey) is string store)
            XpingContext.Initialize(new XpingConfiguration { Mode = XpingMode.LocalOnly, LocalStorePath = store });
    }
}

/// <summary>The test the adapter's test runs in this assembly.</summary>
public sealed class ModuleInitializerProbeTests
{
    /// <summary>Passes.</summary>
    [Fact]
    public void Passes()
    {
    }
}
