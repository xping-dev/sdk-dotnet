/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Collections;
using System.Globalization;
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
/// right-to-left override, a line separator, an <c>é</c> and an emoji. A renderer site that appends a DTO
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
    // An astral rune last, so a cut that splits a surrogate pair prints half of one (Amendment 2E).
    private const string Suffix = "\u001b[31m\u202e\u2028\u00e9\U0001F600";

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
        ReportEnvelope envelope = Hostile(ReportFixtures.Get(fixture), keepVocabulary: true);

        foreach ((string mode, ReportGlyphs glyphs) in Modes)
        {
            foreach (string report in Renderings(envelope, glyphs))
            {
                AssertSafe(report, $"the {mode} rendering of {fixture}");

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
    /// A vocabulary value the renderer does not know is echoed as it was, so it is cleaned too
    /// (Amendment 2F).
    /// </summary>
    /// <remarks>
    /// Only safety is asserted. An unknown kind or severity changes the layout, and what the
    /// renderer makes of one is the vocabulary's business, not D7's.
    /// </remarks>
    [Theory]
    [MemberData(nameof(ReportFixtures.Names), MemberType = typeof(ReportFixtures))]
    public void NoVocabularyEchoReachesTheTerminalUnsafe(string fixture)
    {
        ReportEnvelope envelope = Hostile(ReportFixtures.Get(fixture), keepVocabulary: false);

        foreach ((string mode, ReportGlyphs glyphs) in Modes)
        {
            foreach (string report in Renderings(envelope, glyphs))
                AssertSafe(report, $"the {mode} rendering of {fixture}, vocabulary rewritten");
        }
    }

    /// <summary>
    /// A value cut to fit is never cut through a surrogate pair (Amendment 2E).
    /// </summary>
    /// <remarks>
    /// Placed where each cut falls: the emoji straddles a sha's seventh character, and the start of
    /// what <c>Fit</c> keeps of a latest-run failure too long for its line.
    /// </remarks>
    [Fact]
    public void ACutNeverSplitsASurrogatePair()
    {
        const string sha = "abcdef\U0001F600xyz";

        // The trailer's budget is 55, Fit keeps the last 52, so the cut lands on index 4.
        string summary = "abc\U0001F600" + new string('y', 51);

        ReportEnvelope latest = ReportFixtures.Get("latest-run");
        LatestRunDto run = latest.LatestRun!;

        latest = latest with
        {
            Context = latest.Context! with { Sha = sha },
            LatestRun = run with
            {
                Sha = sha,
                Failures = [run.Failures[0] with { FailureSummary = summary }, .. run.Failures.Skip(1)]
            }
        };

        ReportEnvelope history = ReportFixtures.Get("history-flaky");
        history = history with
        {
            Test = history.Test! with { Sessions = [.. history.Test.Sessions.Select(s => s with { Sha = sha })] }
        };

        foreach (ReportEnvelope envelope in new[] { latest, history })
        {
            string report = Render(envelope, Drawn(ReportGlyphs.Unicode, color: false));

            AssertSafe(report, "a report whose cuts fall inside an emoji");
            Assert.Contains("abcdef", report, StringComparison.Ordinal);
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
            ReportEnvelope envelope = Hostile(ReportFixtures.Get(fixture), keepVocabulary: true);

            return Render(envelope, Drawn(ReportGlyphs.Unicode, color: false))
                .Contains("?[31m??\u00e9\U0001F600", StringComparison.Ordinal);
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
    /// Asserts D7's always-unsafe set is absent, bar the renderer's own line breaks, and that no
    /// surrogate stands alone.
    /// </summary>
    /// <remarks>
    /// Classified per UTF-16 unit with <see cref="char.GetUnicodeCategory(char)"/> rather than
    /// through <see cref="TerminalText"/>, so a wrong classification there cannot make this agree
    /// with it. Every character the rewrite plants is in the BMP but the emoji, which is checked as a
    /// pair.
    /// </remarks>
    private static void AssertSafe(string report, string where)
    {
        for (int i = 0; i < report.Length; i++)
        {
            char c = report[i];

            if (char.IsHighSurrogate(c) && i + 1 < report.Length && char.IsLowSurrogate(report[i + 1]))
            {
                i++;
                continue;
            }

            Assert.True(
                c is '\n' or '\r' || char.GetUnicodeCategory(c) is not (UnicodeCategory.Control or UnicodeCategory.Format
                    or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator or UnicodeCategory.Surrogate),
                $"U+{(int)c:X4} reached {where}");
        }
    }

    /// <summary>A fixture with every string in it made hostile.</summary>
    private static ReportEnvelope Hostile(ReportEnvelope envelope, bool keepVocabulary) =>
        (ReportEnvelope)Rewrite(envelope, memberName: null, keepVocabulary)!;

    /// <summary>
    /// Copies a DTO graph with every string in it made hostile, vocabulary aside.
    /// </summary>
    /// <param name="value">A DTO, a collection of them, a string, or a value left as it is.</param>
    /// <param name="memberName">The property the value was read from, for the vocabulary check.</param>
    /// <param name="keepVocabulary">Whether vocabulary values keep their own.</param>
    /// <returns>The copy.</returns>
    /// <remarks>
    /// By field, not property: a positional record's backing field is writable on a clone whatever
    /// its property's accessors are, and a computed property has no field to rewrite.
    /// </remarks>
    private static object? Rewrite(object? value, string? memberName, bool keepVocabulary)
    {
        switch (value)
        {
            case null:
                return null;

            case string text:
                return keepVocabulary && memberName != null && Vocabulary.Contains(memberName) ? text : text + Suffix;

            case IDictionary dictionary:
                return RewriteDictionary(dictionary, keepVocabulary);

            case IEnumerable sequence when value.GetType().IsGenericType || value.GetType().IsArray:
                return RewriteList(sequence, value.GetType(), keepVocabulary);
        }

        Type type = value.GetType();

        if (type.Namespace != typeof(ReportEnvelope).Namespace)
            return value;

        object copy = CloneMethod.Invoke(value, null)!;

        for (Type? current = type; current != null && current != typeof(object); current = current.BaseType)
        {
            foreach (FieldInfo field in current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                field.SetValue(copy, Rewrite(field.GetValue(value), PropertyName(field), keepVocabulary));
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

    private static object RewriteList(IEnumerable sequence, Type type, bool keepVocabulary)
    {
        Type element = type.IsArray ? type.GetElementType()! : type.GetGenericArguments()[0];
        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(element))!;

        foreach (object? item in sequence)
            list.Add(Rewrite(item, memberName: null, keepVocabulary));

        return list;
    }

    /// <summary>Rewrites keys as well as values: a not-measured kind is a key, and is printed.</summary>
    private static object RewriteDictionary(IDictionary dictionary, bool keepVocabulary)
    {
        Type[] arguments = dictionary.GetType().GetGenericArguments();
        var copy = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(arguments))!;

        foreach (DictionaryEntry entry in dictionary)
            copy[Rewrite(entry.Key, memberName: null, keepVocabulary)!] = Rewrite(entry.Value, memberName: null, keepVocabulary);

        return copy;
    }
}
