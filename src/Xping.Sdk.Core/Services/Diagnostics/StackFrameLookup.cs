/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System;
using System.Collections.Generic;

namespace Xping.Sdk.Core.Services.Diagnostics;

/// <summary>
/// Reads method identifiers out of a stack trace so a failure can be attributed to the member it
/// happened in.
/// </summary>
/// <remarks>
/// <para>
/// Shared by all three adapters because all three need the same thing. None of the frameworks reports
/// where a failure happened: NUnit reports a <c>[SetUp]</c> failure with the same result state as a
/// body failure, MSTest hands over the raw exception with no wrapper, and xUnit marks only its class
/// fixtures. In every other case the stack trace is the sole evidence, so the frame walk belongs in one
/// place rather than in three.
/// </para>
/// <para>
/// Distinct from <c>StackFrameExtractor</c> in the CLI, which selects frames for a failure signature
/// and lives in a different assembly. That one discards framework frames to find something worth
/// grouping on; this one looks for one specific member and keeps the trace's order intact.
/// </para>
/// </remarks>
public static class StackFrameLookup
{
    private static readonly char[] _lineSeparators = ['\r', '\n'];

    // Written by the compiler as "Type..ctor" / "Type..cctor" — two dots, because the method name
    // itself begins with one.
    private static readonly string[] _constructorSuffixes = ["..ctor", "..cctor"];

    // Frames are written "   at Namespace.Type.Method(args) in /file.cs:line 12". The leading token
    // is localized in some runtimes, so the file suffix and the argument list are what get cut,
    // never a match on the word itself.
    private const string FileMarker = " in ";

    /// <summary>
    /// Returns the method identifiers in a stack trace, innermost frame first.
    /// </summary>
    /// <param name="stackTrace">The raw trace, or <see langword="null"/>.</param>
    /// <returns>
    /// Identifiers of the form <c>Namespace.Type.Method</c>, in the order the trace lists them.
    /// Empty when there is nothing to read.
    /// </returns>
    /// <remarks>
    /// Compiler-generated frames are rewritten back to the method the author wrote: an async
    /// <c>[SetUp]</c> appears as <c>Type+&lt;Setup&gt;d__1.MoveNext</c>, and matching that against a
    /// reflected method name would fail for exactly the lifecycle members most likely to be async.
    /// </remarks>
    public static IEnumerable<string> Frames(string? stackTrace)
    {
        if (string.IsNullOrWhiteSpace(stackTrace))
            yield break;

        // Non-null after the check above.
        string[] lines = stackTrace!.Split(_lineSeparators, StringSplitOptions.RemoveEmptyEntries);

        foreach (string line in lines)
        {
            string? frame = ParseFrame(line);
            if (frame != null)
                yield return frame;
        }
    }

    /// <summary>
    /// Returns the first frame that names one of the given members.
    /// </summary>
    /// <param name="stackTrace">The raw trace, or <see langword="null"/>.</param>
    /// <param name="candidates">Identifiers of the form <c>Namespace.Type.Method</c>.</param>
    /// <returns>The matching identifier, or <see langword="null"/> when none matched.</returns>
    /// <remarks>
    /// Innermost frame wins. A <c>[SetUp]</c> that calls a helper which throws puts the helper on top
    /// and the setup method below it, and the answer wanted is still the setup method — but a setup
    /// method that itself invokes the test body would put the body on top, and there the innermost
    /// frame is the honest answer. Ordering by depth keeps the nearest cause rather than the outermost.
    /// </remarks>
    public static string? FirstMatch(string? stackTrace, ICollection<string> candidates)
    {
        if (candidates == null || candidates.Count == 0)
            return null;

        foreach (string frame in Frames(stackTrace))
        {
            if (candidates.Contains(frame))
                return frame;
        }

        return null;
    }

    /// <summary>
    /// Shortens a qualified identifier to <c>Type.Method</c> for display.
    /// </summary>
    /// <param name="qualified">An identifier of the form <c>Namespace.Type.Method</c>.</param>
    /// <returns>The last two segments, or the input when it has fewer.</returns>
    /// <remarks>
    /// The namespace is dropped because the record already carries the owning class in
    /// <c>TestOrchestrationRecord.CollectionName</c>; repeating it here would push the member name off
    /// the end of a report line to say something already said.
    /// </remarks>
    public static string Shorten(string qualified)
    {
        if (string.IsNullOrEmpty(qualified))
            return qualified;

        // A constructor is written "Namespace.Type..ctor". Splitting on the last two dots would
        // return ".ctor" and throw away the one part a reader needs, so the type is taken explicitly.
        foreach (string suffix in _constructorSuffixes)
        {
            if (qualified.EndsWith(suffix, StringComparison.Ordinal))
                return Simple(qualified.Substring(0, qualified.Length - suffix.Length)) + suffix;
        }

        int method = qualified.LastIndexOf('.');
        if (method <= 0)
            return qualified;

        int type = qualified.LastIndexOf('.', method - 1);
        return type < 0 ? qualified : qualified.Substring(type + 1);
    }

    /// <summary>
    /// Returns the type a frame belongs to, or <see langword="null"/> when it names no type.
    /// </summary>
    /// <param name="frame">An identifier as returned by <see cref="Frames"/>.</param>
    /// <returns>The qualified type name.</returns>
    /// <remarks>
    /// xUnit needs this to tell a test class constructor from a collection fixture constructor: both
    /// arrive as a bare exception with a <c>..ctor</c> frame on top, and the only thing separating
    /// "this test's own setup failed" from "shared state everything in the collection depends on
    /// failed" is which type the constructor belongs to.
    /// </remarks>
    public static string? DeclaringType(string? frame)
    {
        if (string.IsNullOrEmpty(frame))
            return null;

        // Non-null after the check above.
        foreach (string suffix in _constructorSuffixes)
        {
            if (frame!.EndsWith(suffix, StringComparison.Ordinal))
                return frame.Substring(0, frame.Length - suffix.Length);
        }

        int method = frame!.LastIndexOf('.');
        return method <= 0 ? null : frame.Substring(0, method);
    }

    /// <summary>
    /// Returns the last non-empty name enclosed in angle brackets, or <see langword="null"/>.
    /// </summary>
    private static string? LastBracketedName(string frame, int from)
    {
        string? found = null;

        for (int i = from; i >= 0 && i < frame.Length; i = frame.IndexOf('<', i + 1))
        {
            int close = frame.IndexOf('>', i + 1);
            if (close < 0)
                break;

            // "<>" fronts a closure class and names nothing; the method's own name follows it.
            if (close > i + 1)
                found = frame.Substring(i + 1, close - i - 1);
        }

        return found;
    }

    private static string Simple(string qualified)
    {
        int dot = qualified.LastIndexOf('.');
        return dot < 0 ? qualified : qualified.Substring(dot + 1);
    }

    private static string? ParseFrame(string line)
    {
        string text = line.Trim();
        if (text.Length == 0)
            return null;

        // Drop the file and line suffix before anything else: a path can contain parentheses and
        // would otherwise truncate the identifier at the wrong place.
        int file = text.IndexOf(FileMarker, StringComparison.Ordinal);
        if (file >= 0)
            text = text.Substring(0, file);

        int arguments = text.IndexOf('(');
        if (arguments >= 0)
            text = text.Substring(0, arguments);

        // "at " (or its localized equivalent) sits in front of the identifier. Everything up to the
        // last space is prefix, since an identifier never contains one.
        int prefix = text.LastIndexOf(' ');
        if (prefix >= 0)
            text = text.Substring(prefix + 1);

        text = text.Trim();
        if (text.Length == 0)
            return null;

        return Normalize(text);
    }

    /// <summary>
    /// Rewrites a compiler-generated identifier back to the method the author declared.
    /// </summary>
    private static string Normalize(string frame)
    {
        // Async and iterator methods compile to a nested type: "Type+<Setup>d__1.MoveNext", and
        // lambdas to "Type.<>c__DisplayClass4_0.<Setup>b__0". Both name the original method between
        // angle brackets — but the lambda form also carries an empty "<>" pair in front of the
        // closure class, so the first bracket pair is not necessarily the informative one.
        int first = frame.IndexOf('<');
        if (first >= 0)
        {
            string? declared = LastBracketedName(frame, first);
            if (declared != null)
            {
                // Everything in front of the first generated segment is the type the author wrote.
                string owner = frame.Substring(0, first).TrimEnd('.', '+');
                frame = owner.Length == 0 ? declared : owner + "." + declared;
            }
        }

        return Canonicalize(frame);
    }

    /// <summary>
    /// Builds the identifier a reflected member would appear under in a stack trace.
    /// </summary>
    /// <param name="declaringTypeFullName">The declaring type's <c>FullName</c>.</param>
    /// <param name="methodName">The method's name.</param>
    /// <returns>An identifier comparable against <see cref="Frames"/> output.</returns>
    /// <remarks>
    /// Callers reflect their framework's lifecycle attributes and need the result to match what the
    /// runtime prints. Reflection spells a nested type <c>Outer+Inner</c> and a generic one
    /// <c>Type`1</c>; going through here rather than concatenating by hand is what stops a fixture
    /// declared inside another class from silently never matching.
    /// </remarks>
    public static string Member(string? declaringTypeFullName, string methodName) =>
        Canonicalize((declaringTypeFullName ?? string.Empty) + "." + methodName);

    /// <summary>
    /// Strips the spellings that differ between reflection and a printed frame.
    /// </summary>
    private static string Canonicalize(string identifier) =>
        identifier
            .Replace('+', '.')
            .Replace("`1", string.Empty)
            .Replace("`2", string.Empty)
            .Replace("`3", string.Empty);
}
