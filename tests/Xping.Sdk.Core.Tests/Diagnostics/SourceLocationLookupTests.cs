/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Collections;
using System.Reflection;
using System.Reflection.Emit;
using Xping.Sdk.Core.Services.Diagnostics;

namespace Xping.Sdk.Core.Tests.Diagnostics;

/// <summary>
/// Covers reading a method's declaration site out of the Portable PDB.
/// </summary>
/// <remarks>
/// <para>
/// These tests read this assembly's own symbols and reflect over the methods in this very file, so
/// they exercise the real thing rather than a fixture: if the PDB walk is wrong, or a compiler shape
/// like an async state machine is not followed, these fail. The trade is that the expected line
/// numbers are the ones written in this file, so <b>moving a probe method changes its answer</b> —
/// which is why each is asserted against a marker captured next to its own body rather than a
/// hard-coded constant.
/// </para>
/// <para>
/// The line a PDB reports is the body's opening brace, so every probe declares its marker as the
/// first thing inside the body and the assertions compare against that.
/// </para>
/// </remarks>
public sealed class SourceLocationLookupTests
{
    private const string ThisFile = "SourceLocationLookupTests.cs";

    private const BindingFlags Probes = BindingFlags.NonPublic | BindingFlags.Static;

    // ---------------------------------------------------------------------------
    // Of
    // ---------------------------------------------------------------------------

    [Fact]
    public void ANullMethodResolvesToNothingRatherThanThrowing()
    {
        (string? file, int? line) = SourceLocationLookup.Of(null);

        Assert.Null(file);
        Assert.Null(line);
    }

    [Fact]
    public void AnOrdinaryMethodResolvesToTheFileItIsDeclaredIn()
    {
        (string? file, int? line) = Of(nameof(SyncProbe));

        Assert.NotNull(file);
        Assert.EndsWith(ThisFile, file, StringComparison.Ordinal);
        Assert.NotNull(line);
        Assert.Equal(SyncProbe(), line);
    }

    /// <summary>
    /// The state-machine redirect. Without it an async method's kickoff carries only hidden
    /// sequence points and this resolves to nothing — which would lose exactly the tests most
    /// likely to be worth locating.
    /// </summary>
    [Fact]
    public async Task AnAsyncMethodResolvesToItsOwnBodyAndNotItsStateMachine()
    {
        (string? file, int? line) = Of(nameof(AsyncProbe));

        Assert.NotNull(file);
        Assert.EndsWith(ThisFile, file, StringComparison.Ordinal);
        Assert.Equal(await AsyncProbe(), line);
    }

    [Fact]
    public void AnIteratorMethodResolvesToItsOwnBody()
    {
        (string? file, int? line) = Of(nameof(IteratorProbe));

        Assert.NotNull(file);
        Assert.EndsWith(ThisFile, file, StringComparison.Ordinal);
        Assert.Equal(IteratorProbe().First(), line);
    }

    [Fact]
    public void AGenericMethodResolvesToItsOwnBody()
    {
        (string? file, int? line) = Of(nameof(GenericProbe));

        Assert.NotNull(file);
        Assert.EndsWith(ThisFile, file, StringComparison.Ordinal);
        Assert.Equal(GenericProbe<string>(), line);
    }

    [Fact]
    public void AMethodOnANestedTypeResolvesToItsOwnBody()
    {
        MethodInfo method = typeof(Nested).GetMethod(nameof(Nested.Probe))!;

        (string? file, int? line) = SourceLocationLookup.Of(method);

        Assert.NotNull(file);
        Assert.EndsWith(ThisFile, file, StringComparison.Ordinal);
        Assert.Equal(Nested.Probe(), line);
    }

    /// <summary>
    /// A dynamic method has no metadata token to look up. It must come back empty rather than
    /// throwing, because the same swallow protects every other shape the runtime can produce.
    /// </summary>
    [Fact]
    public void AMethodWithNoMetadataResolvesToNothingRatherThanThrowing()
    {
        var dynamic = new DynamicMethod("Probe", typeof(void), Type.EmptyTypes);
        ILGenerator il = dynamic.GetILGenerator();
        il.Emit(System.Reflection.Emit.OpCodes.Ret);

        (string? file, int? line) = SourceLocationLookup.Of(dynamic);

        Assert.Null(file);
        Assert.Null(line);
    }

    [Fact]
    public void RepeatedLookupsOfOneMethodAgree()
    {
        Assert.Equal(Of(nameof(SyncProbe)), Of(nameof(SyncProbe)));
    }

    /// <summary>
    /// A parallelised suite resolves many methods at once against one shared PDB reader, which is
    /// the condition this runs under in practice.
    /// </summary>
    [Fact]
    public void ConcurrentLookupsAgreeWithSequentialOnes()
    {
        MethodInfo[] methods =
        [
            typeof(SourceLocationLookupTests).GetMethod(nameof(SyncProbe), Probes)!,
            typeof(SourceLocationLookupTests).GetMethod(nameof(AsyncProbe), Probes)!,
            typeof(SourceLocationLookupTests).GetMethod(nameof(IteratorProbe), Probes)!,
            typeof(SourceLocationLookupTests).GetMethod(nameof(GenericProbe), Probes)!
        ];

        (string?, int?)[] expected = methods.Select(SourceLocationLookup.Of).ToArray();

        var actual = new (string?, int?)[methods.Length * 32];
        Parallel.For(0, actual.Length, i => actual[i] = SourceLocationLookup.Of(methods[i % methods.Length]));

        for (int i = 0; i < actual.Length; i++)
            Assert.Equal(expected[i % methods.Length], actual[i]);
    }

    // ---------------------------------------------------------------------------
    // Path shape
    // ---------------------------------------------------------------------------

    /// <summary>
    /// The path is what a reader acts on, so it must not carry the layout of whichever machine
    /// compiled the assembly.
    /// </summary>
    [Fact]
    public void ThePathIsRepositoryRelativeAndUsesForwardSlashes()
    {
        (string? file, _) = Of(nameof(SyncProbe));

        Assert.NotNull(file);
        Assert.DoesNotContain('\\', file);
        Assert.False(Path.IsPathRooted(file), $"Expected a repository-relative path, got '{file}'.");
        Assert.StartsWith("tests/Xping.Sdk.Core.Tests/", file, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------
    // Relativize
    // ---------------------------------------------------------------------------

    /// <summary>
    /// The deterministic root is the one case that survives the assembly being built somewhere other
    /// than where it runs, since every other route probes the filesystem.
    /// </summary>
    [Fact]
    public void ADeterministicBuildPathIsStrippedToItsRepositoryRelativePart()
    {
        Assert.Equal(
            "tests/Cart/CartTests.cs",
            SourceLocationLookup.Relativize("/_/tests/Cart/CartTests.cs", repositoryRoot: null));
    }

    [Fact]
    public void APathUnderTheRepositoryRootIsMadeRelativeToIt()
    {
        Assert.Equal(
            "tests/Cart/CartTests.cs",
            SourceLocationLookup.Relativize("/build/shop/tests/Cart/CartTests.cs", "/build/shop"));
    }

    [Fact]
    public void AWindowsPathIsMadeRelativeAndNormalized()
    {
        Assert.Equal(
            "tests/Cart/CartTests.cs",
            SourceLocationLookup.Relativize(@"C:\build\shop\tests\Cart\CartTests.cs", @"C:\build\shop"));
    }

    /// <summary>
    /// A path built on another machine has no root to hang off. It is reported verbatim rather than
    /// dropped: an absolute path a reader has to translate still beats no location at all.
    /// </summary>
    [Fact]
    public void APathOutsideAnyKnownRootIsKeptVerbatim()
    {
        Assert.Equal(
            "/elsewhere/agent/work/CartTests.cs",
            SourceLocationLookup.Relativize("/elsewhere/agent/work/CartTests.cs", "/build/shop"));
    }

    [Fact]
    public void APathThatMerelySharesAPrefixWithTheRootIsNotTruncated()
    {
        // "/build/shop-legacy" is not inside "/build/shop", and a naive prefix test would report it
        // as "-legacy/tests/CartTests.cs".
        Assert.Equal(
            "/build/shop-legacy/tests/CartTests.cs",
            SourceLocationLookup.Relativize("/build/shop-legacy/tests/CartTests.cs", "/build/shop"));
    }

    // ---------------------------------------------------------------------------
    // Probes
    //
    // Each returns the line its own body opens on, which is what the PDB records. Declared here so
    // an edit that moves them keeps the assertions honest.
    // ---------------------------------------------------------------------------

    private static int SyncProbe()
    {   // <- the opening brace is the line a PDB reports
        return Line();
    }

    private static async Task<int> AsyncProbe()
    {
        int line = Line();
        await Task.Yield();
        return line;
    }

    private static IEnumerable<int> IteratorProbe()
    {
        yield return Line();
    }

    private static int GenericProbe<T>()
    {
        return Line();
    }

    private static class Nested
    {
        public static int Probe()
        {
            return Line();
        }
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Returns the line the calling method's body opens on.
    /// </summary>
    /// <remarks>
    /// <c>[CallerLineNumber]</c> gives the line of the <i>call</i>, and every probe calls this on the
    /// first line of its body — one line below the opening brace the PDB reports.
    /// </remarks>
    private static int Line([System.Runtime.CompilerServices.CallerLineNumber] int line = 0) => line - 1;

    private static (string? File, int? Line) Of(string name) =>
        SourceLocationLookup.Of(typeof(SourceLocationLookupTests).GetMethod(name, Probes)!);
}
