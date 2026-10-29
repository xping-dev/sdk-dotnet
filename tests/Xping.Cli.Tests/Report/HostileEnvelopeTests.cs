/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Collections;
using System.Reflection;
using Xping.Cli.Report.Contract;
using Xping.Cli.Report.Rendering;
using Xping.Cli.Reporting;

using static Xping.Cli.Tests.Report.ReportText;

namespace Xping.Cli.Tests.Report;

/// <summary>
/// Every recorded string the text renderers print is made safe for the terminal (D7).
/// </summary>
/// <remarks>
/// <para>
/// Not a list of fields. Every fixture envelope has every string in its DTO graph rewritten — each
/// property, each list element, each dictionary key and value — to end in an escape sequence, a
/// right-to-left override, a line separator and an <c>é</c>. A renderer site that appends a DTO
/// string without <c>Recorded</c> or <c>Command</c> lets one of them through, and this fails on it
/// whatever field it is and however recently it was added.
/// </para>
/// <para>
/// Values the renderer maps through <see cref="ReportVocabulary"/> keep their own. A fixture with an
/// unknown kind or severity tests the vocabulary's fallback, not recorded text.
/// </para>
/// </remarks>
public sealed class HostileEnvelopeTests
{
    private const string Hostile = "\u001b[31m\u202e\u2028\u00e9";

    /// <summary>The properties whose values are vocabulary, not recorded text.</summary>
    private static readonly HashSet<string> Vocabulary = new(StringComparer.Ordinal)
    {
        nameof(FindingDto.Kind),
        nameof(FindingDto.Severity),
        nameof(FindingDto.Population),
        nameof(LatestRunFailureDto.Status)
    };

    private static readonly (string Mode, ReportGlyphs Glyphs)[] Modes =
    [
        ("ascii", ReportGlyphs.Ascii),
        ("unicode", ReportGlyphs.Unicode)
    ];

    [Theory]
    [MemberData(nameof(ReportFixtures.Names), MemberType = typeof(ReportFixtures))]
    public void NoRecordedStringReachesTheTerminalUnsafe(string fixture)
    {
        ReportEnvelope envelope = (ReportEnvelope)Rewrite(ReportFixtures.Get(fixture), memberName: null)!;

        foreach ((string mode, ReportGlyphs glyphs) in Modes)
        {
            foreach (string report in Renderings(envelope, glyphs))
            {
                Assert.All(report, c => Assert.True(
                    !IsUnsafe(c) || c is '\n' or '\r',
                    $"U+{(int)c:X4} reached the {mode} rendering of {fixture}"));

                if (glyphs != ReportGlyphs.Ascii)
                    continue;

                // A command keeps a non-ASCII character it carries from a recorded name: replacing it
                // would make the command select nothing. Everything else is ASCII.
                string rest = Commands(envelope)
                    .Select(command => TerminalText.Printable(command, TerminalCharset.Command))
                    .OrderByDescending(command => command.Length)
                    .Aggregate(report, (text, command) => text.Replace(command, string.Empty, StringComparison.Ordinal));

                Assert.All(rest, c => Assert.True(
                    c < 0x80, $"non-ASCII U+{(int)c:X4} outside a command in the ascii rendering of {fixture}"));
            }
        }
    }

    /// <summary>
    /// The rewrite reaches the output at all, so the test above is not passing on nothing.
    /// </summary>
    [Fact]
    public void TheRewrittenTextIsPrinted()
    {
        int printed = ReportFixtures.Keys.Count(fixture =>
        {
            var envelope = (ReportEnvelope)Rewrite(ReportFixtures.Get(fixture), memberName: null)!;

            return Render(envelope, Drawn(ReportGlyphs.Unicode, color: false))
                .Contains("?[31m??\u00e9", StringComparison.Ordinal);
        });

        Assert.Equal(ReportFixtures.Keys.Count, printed);
    }

    /// <summary>The text renderers' output for an envelope in one glyph set.</summary>
    private static IEnumerable<string> Renderings(ReportEnvelope envelope, ReportGlyphs glyphs)
    {
        yield return Render(envelope, Drawn(glyphs, color: false));

        using var writer = new StringWriter();
        new SummaryReportRenderer(glyphs.Charset).Render(envelope, writer);
        yield return writer.ToString();
    }

    /// <summary>Every command the envelope carries.</summary>
    private static IEnumerable<string> Commands(ReportEnvelope envelope)
    {
        yield return envelope.Truncated.Command;

        if (envelope.Selection?.History is { } history)
            yield return history;

        foreach (FindingDto finding in envelope.Findings)
        {
            yield return finding.DrillDown;

            if (finding.Repro?.Command is { } repro)
                yield return repro;
        }

        foreach (TestCandidateDto candidate in envelope.Test?.Candidates ?? [])
            yield return candidate.Command;
    }

    /// <summary>
    /// D7's always-unsafe set, written out here rather than read from <see cref="TerminalText"/>, so
    /// a wrong classification there cannot make this agree with it.
    /// </summary>
    private static bool IsUnsafe(char c) =>
        c is <= '\u001f' or (>= '\u007f' and <= '\u009f') or '\u00ad' or '\u061c' or '\u2028' or '\u2029' or '\ufeff'
            or (>= '\u200b' and <= '\u200f') or (>= '\u202a' and <= '\u202e') or (>= '\u2060' and <= '\u2064')
            or (>= '\u2066' and <= '\u2069');

    /// <summary>
    /// Copies a DTO graph with every string in it made hostile, vocabulary aside.
    /// </summary>
    /// <param name="value">A DTO, a collection of them, a string, or a value left as it is.</param>
    /// <param name="memberName">The property the value was read from, for the vocabulary check.</param>
    /// <returns>The copy.</returns>
    /// <remarks>
    /// By field, not property: a positional record's backing field is writable on a clone whatever
    /// its property's accessors are, and a computed property has no field to rewrite.
    /// </remarks>
    private static object? Rewrite(object? value, string? memberName)
    {
        switch (value)
        {
            case null:
                return null;

            case string text:
                return memberName != null && Vocabulary.Contains(memberName) ? text : text + Hostile;

            case IDictionary dictionary:
                return RewriteDictionary(dictionary);

            case IEnumerable sequence when value.GetType().IsGenericType || value.GetType().IsArray:
                return RewriteList(sequence, value.GetType());
        }

        Type type = value.GetType();

        if (type.Namespace != typeof(ReportEnvelope).Namespace)
            return value;

        object copy = CloneMethod.Invoke(value, null)!;

        for (Type? current = type; current != null && current != typeof(object); current = current.BaseType)
        {
            foreach (FieldInfo field in current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                field.SetValue(copy, Rewrite(field.GetValue(value), PropertyName(field)));
        }

        return copy;
    }

    private static readonly MethodInfo CloneMethod =
        typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!;

    /// <summary><c>&lt;Kind&gt;k__BackingField</c> to <c>Kind</c>.</summary>
    private static string PropertyName(FieldInfo field) =>
        field.Name is ['<', .. var rest] && rest.IndexOf('>', StringComparison.Ordinal) is var end and > 0
            ? rest[..end]
            : field.Name;

    private static object RewriteList(IEnumerable sequence, Type type)
    {
        Type element = type.IsArray ? type.GetElementType()! : type.GetGenericArguments()[0];
        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(element))!;

        foreach (object? item in sequence)
            list.Add(Rewrite(item, memberName: null));

        return list;
    }

    /// <summary>Rewrites keys as well as values: a not-measured kind is a key, and is printed.</summary>
    private static object RewriteDictionary(IDictionary dictionary)
    {
        Type[] arguments = dictionary.GetType().GetGenericArguments();
        var copy = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(arguments))!;

        foreach (DictionaryEntry entry in dictionary)
            copy[Rewrite(entry.Key, memberName: null)!] = Rewrite(entry.Value, memberName: null);

        return copy;
    }
}
