/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Xunit;

namespace Xping.Sdk.XUnit.ThrowingProbe;

/// <summary>
/// A test assembly whose module initializer throws. Its own assembly, because the runtime runs a module
/// initializer once per process, and one that failed leaves its module unusable. The test that runs it
/// must not name a type of this assembly in code: the runtime runs the initializer as soon as a method
/// that does is compiled.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "A test fixture the adapter's tests run, not code under test.")]
public static class ThrowingModuleInitializerProbe
{
    /// <summary>The message the initializer throws with.</summary>
    public const string Message = "probe module initializer failed";

    // CA2255: a module initializer in a library is what this probe exists to exercise.
#pragma warning disable CA2255
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void Initialize() => throw new InvalidOperationException(Message);
}

/// <summary>The test the adapter's test runs in this assembly.</summary>
[ExcludeFromCodeCoverage(Justification = "A test fixture the adapter's tests run, not code under test.")]
public sealed class ThrowingModuleInitializerProbeTests
{
    /// <summary>Would pass, if its module had initialized.</summary>
    [Fact]
    public void Passes()
    {
    }
}
