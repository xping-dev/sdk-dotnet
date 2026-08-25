/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Xping.Sdk.Core.Services.Diagnostics;

/// <summary>
/// Finds the file and line a test method is declared at, by reading the assembly's Portable PDB.
/// </summary>
/// <remarks>
/// <para>
/// Shared by all three adapters because none of the frameworks answers this. xUnit exposes
/// <c>ITestCase.SourceInformation</c>, but only when the runner opted into source discovery; NUnit's
/// <c>ITest</c> and MSTest's <c>TestContext</c> expose nothing at all. What all three do have is a
/// real <see cref="MethodInfo"/> for the test, and a method's metadata token is enough to find its
/// sequence points — the same route VSTest's <c>DiaSession</c> takes to populate Test Explorer.
/// </para>
/// <para>
/// The line reported is the method body's opening brace, not the <c>[Test]</c> attribute and not the
/// signature. A PDB records where code is, and an attribute is not code; the first sequence point of
/// a method body is the nearest true thing available. This matches what <c>DiaSession</c> returns, so
/// a test navigated to from a report lands where the IDE would have put it.
/// </para>
/// <para>
/// Every failure is silent. A missing PDB, a <c>DebugType=none</c> build, a single-file host with no
/// assembly location, a dynamic method with no token — each yields two nulls, and the report simply
/// omits the location. Source location is a convenience laid over a test run and must never be able
/// to disturb one.
/// </para>
/// </remarks>
public static class SourceLocationLookup
{
    // The root a deterministic build rewrites source paths to when ContinuousIntegrationBuild is set
    // and no SourceRoot maps the path. What follows it is already repository-relative.
    private const string DeterministicRoot = "/_/";

    // Per-method results. A [TestCase]/[Theory]/[DataRow] method arrives once per case, and the blob
    // walk below should happen once per method rather than once per execution.
    private static readonly ConcurrentDictionary<MethodInfo, (string?, int?)> _locations = new();

    // Per-assembly readers. Held for the process lifetime deliberately: the MetadataReader borrows
    // memory owned by its provider, so disposing the provider would invalidate every reader handed
    // out from it. One mapped PDB per test assembly is a bounded cost.
    //
    // Lazy rather than a bare value, because GetOrAdd may run its factory on several threads at once
    // and keep only one result. For the per-method cache above that costs a repeated computation;
    // here it would open the PDB more than once and drop the losing handle without disposing it,
    // which under a parallelised suite is a file handle leak rather than a wasted cycle.
    private static readonly ConcurrentDictionary<Assembly, Lazy<AssemblyDebugInfo?>> _assemblies = new();

    /// <summary>
    /// Returns the source file and line the given method is declared at.
    /// </summary>
    /// <param name="method">The test method, or <see langword="null"/>.</param>
    /// <returns>
    /// The file path and line, or two nulls when no debug information could be read. The path is
    /// relative to the repository root when one could be determined, and uses forward slashes so an
    /// assembly built on Windows and one built on Linux read identically.
    /// </returns>
    public static (string? File, int? Line) Of(MethodInfo? method)
    {
        if (method == null)
            return (null, null);

        return _locations.GetOrAdd(method, Resolve);
    }

    private static (string?, int?) Resolve(MethodInfo method)
    {
        try
        {
            // An async or iterator test has its body moved into a state machine; the method the
            // author wrote keeps only hidden sequence points. Following the redirect matters more
            // than it sounds — the tests most likely to be flaky are the ones most likely to be
            // async, and they are exactly the ones this would otherwise lose.
            MethodInfo target = StateMachineBodyOf(method) ?? method;

            AssemblyDebugInfo? debug = DebugInfoOf(target.Module.Assembly);
            if (debug == null)
                return (null, null);

            return SequencePointOf(debug, target.MetadataToken);
        }
        catch (Exception)
        {
            // Reflection over a method whose declaring assembly cannot be fully loaded, a token a
            // dynamic method does not have, a PDB that does not match the assembly: none of them is
            // worth failing a test run over.
            return (null, null);
        }
    }

    /// <summary>
    /// Returns the <c>MoveNext</c> the compiler moved a method's body into, when it moved one.
    /// </summary>
    private static MethodInfo? StateMachineBodyOf(MethodInfo method)
    {
        Type? stateMachine = method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType
            ?? method.GetCustomAttribute<IteratorStateMachineAttribute>()?.StateMachineType;

        return stateMachine?.GetMethod(
            "MoveNext",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
    }

    /// <summary>
    /// Reads the first visible sequence point of a method, translated to a reportable path.
    /// </summary>
    private static (string?, int?) SequencePointOf(AssemblyDebugInfo debug, int metadataToken)
    {
        MethodDefinitionHandle definition = MetadataTokens.MethodDefinitionHandle(metadataToken);
        MethodDebugInformation information = debug.Reader.GetMethodDebugInformation(
            definition.ToDebugInformationHandle());

        if (information.SequencePointsBlob.IsNil)
            return (null, null);

        DocumentHandle document = default;
        int line = 0;
        bool found = false;

        foreach (SequencePoint point in information.GetSequencePoints())
        {
            // A hidden point marks compiler-generated code and carries the sentinel line 0xFEEFEE.
            // Taking one would report a line no reader could find in the file.
            if (point.IsHidden)
                continue;

            // The lowest line rather than the first point in IL order: a state machine's MoveNext
            // begins at the resume dispatch, which the compiler maps back to a point inside the
            // body rather than at its start.
            if (!found || point.StartLine < line)
            {
                document = point.Document;
                line = point.StartLine;
                found = true;
            }
        }

        if (!found || document.IsNil)
            return (null, null);

        string path = debug.Reader.GetString(debug.Reader.GetDocument(document).Name);

        return (Relativize(path, debug.RepositoryRoot), line);
    }

    /// <summary>
    /// Turns the absolute path a PDB records into one that reads the same on any machine.
    /// </summary>
    /// <param name="path">The path as the PDB spells it.</param>
    /// <param name="repositoryRoot">The repository root, when one was found.</param>
    /// <returns>A repository-relative path where possible, otherwise the input.</returns>
    /// <remarks>
    /// A PDB records the path of the machine that compiled the assembly. Printing
    /// <c>/home/runner/work/shop/shop/tests/CartTests.cs</c> in a report read on a laptop is noise,
    /// and uploading it discloses the build agent's layout for nothing. The deterministic root is
    /// checked first because it is the one case that survives the assembly being built somewhere
    /// other than where it runs — nothing else can, since the other routes probe the filesystem.
    /// </remarks>
    public static string Relativize(string path, string? repositoryRoot)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        string normalized = path.Replace('\\', '/');

        if (normalized.StartsWith(DeterministicRoot, StringComparison.Ordinal))
            return normalized.Substring(DeterministicRoot.Length);

        // The root of the tree the source file itself lives in, which is the right answer whenever
        // the assembly was built on the machine now reading it.
        string? root = RepositoryRoot.Find(DirectoryOf(path)) ?? repositoryRoot;
        if (root == null)
            return normalized;

        string prefix = root.Replace('\\', '/').TrimEnd('/') + "/";

        // Ordinal rather than a culture-aware or case-insensitive compare: a path that differs only
        // by case is a different path on the platforms most likely to be running this in CI, and
        // guessing wrong produces a path relative to the wrong root rather than no path at all.
        return normalized.StartsWith(prefix, StringComparison.Ordinal)
            ? normalized.Substring(prefix.Length)
            : normalized;
    }

    private static string? DirectoryOf(string path)
    {
        try
        {
            return Path.GetDirectoryName(path);
        }
        catch (ArgumentException)
        {
            // A path spelled for another platform can carry characters this one rejects.
            return null;
        }
    }

    private static AssemblyDebugInfo? DebugInfoOf(Assembly assembly) =>
        _assemblies.GetOrAdd(
            assembly,
            static key => new Lazy<AssemblyDebugInfo?>(
                () => OpenDebugInfo(key), LazyThreadSafetyMode.ExecutionAndPublication)).Value;

    private static AssemblyDebugInfo? OpenDebugInfo(Assembly assembly)
    {
        try
        {
            string location = assembly.Location;

            // Single-file hosts and assemblies loaded from memory report no location, and there is
            // no file to look beside.
            if (string.IsNullOrEmpty(location) || !File.Exists(location))
                return null;

            var peReader = new PEReader(File.OpenRead(location));

            // Covers both shapes in one call: a .pdb sitting beside the assembly, and one embedded
            // into the assembly by DebugType=embedded. It also honours the path recorded in the
            // debug directory, so an out-of-tree symbol file is found where a guess would miss it.
            if (!peReader.TryOpenAssociatedPortablePdb(location, File.OpenRead, out MetadataReaderProvider? provider, out _)
                || provider == null)
            {
                peReader.Dispose();
                return null;
            }

            return new AssemblyDebugInfo(
                peReader,
                provider,
                provider.GetMetadataReader(),
                RepositoryRoot.Find(Path.GetDirectoryName(location)));
        }
        catch (Exception ex) when (
            ex is IOException
                or UnauthorizedAccessException
                or BadImageFormatException
                or NotSupportedException
                or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// A test assembly's debug metadata, plus the repository it was found in.
    /// </summary>
    /// <remarks>
    /// The root is resolved once here rather than per method: it is a filesystem walk, and every
    /// method in an assembly resolves to the same answer.
    /// </remarks>
    private sealed class AssemblyDebugInfo
    {
        // Neither is read after construction. They are held so that the memory the MetadataReader
        // borrows stays owned for as long as the reader is reachable: an embedded PDB is read
        // through a block the PEReader owns, and finalizing either one would leave the reader
        // pointing at freed memory.
        private readonly PEReader _peReader;
        private readonly MetadataReaderProvider _provider;

        public AssemblyDebugInfo(
            PEReader peReader,
            MetadataReaderProvider provider,
            MetadataReader reader,
            string? repositoryRoot)
        {
            _peReader = peReader;
            _provider = provider;
            Reader = reader;
            RepositoryRoot = repositoryRoot;
        }

        /// <summary>Gets the reader over the assembly's Portable PDB.</summary>
        public MetadataReader Reader { get; }

        /// <summary>Gets the repository root the assembly was built in, when known.</summary>
        public string? RepositoryRoot { get; }
    }
}
