/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Reporting;

namespace Xping.Cli.Tests.Reporting;

/// <summary>
/// D7's character classes, each under each charset.
/// </summary>
public sealed class TerminalTextTests
{
    /// <summary>The characters D7 replaces in every charset, one per class boundary.</summary>
    public static TheoryData<string, string> AlwaysUnsafe() => new()
    {
        { "\u0000", "NUL" },
        { "\t", "tab" },
        { "\n", "line feed" },
        { "\r", "carriage return" },
        { "\u001b", "ESC" },
        { "\u001f", "last C0" },
        { "\u007f", "DEL" },
        { "\u0080", "first C1" },
        { "\u009b", "CSI" },
        { "\u009f", "last C1" },
        { "\u2028", "line separator" },
        { "\u2029", "paragraph separator" },
        { "\u200e", "LRM" },
        { "\u200f", "RLM" },
        { "\u061c", "ALM" },
        { "\u202a", "LRE" },
        { "\u202e", "RLO" },
        { "\u2066", "LRI" },
        { "\u2069", "PDI" },
        { "\u200b", "zero-width space" },
        { "\u200d", "zero-width joiner" },
        { "\u2060", "word joiner" },
        { "\u2064", "invisible plus" },
        { "\ufeff", "BOM" },
        { "\u00ad", "soft hyphen" }
    };

    /// <summary>Characters next to an unsafe range that every charset but ASCII keeps.</summary>
    public static TheoryData<string, string> PrintableNonAscii() => new()
    {
        { "\u00a0", "first after C1" },
        { "\u00e9", "\u00e9" },
        { "\u200a", "hair space, before the zero-width range" },
        { "\u2027", "hyphenation point, before the separators" },
        { "\u2065", "unassigned, between the operators and the isolates" },
        { "\u206a", "after the isolates" },
        { "\u2192", "an arrow the glyphs use" }
    };

    [Theory]
    [MemberData(nameof(AlwaysUnsafe))]
    public void AnAlwaysUnsafeCharacterIsReplacedInEveryCharset(string character, string name)
    {
        foreach (TerminalCharset charset in Enum.GetValues<TerminalCharset>())
            Assert.True("a?b" == TerminalText.Printable($"a{character}b", charset), $"{name} under {charset}");
    }

    [Theory]
    [MemberData(nameof(PrintableNonAscii))]
    public void APrintableNonAsciiCharacterIsKeptExceptUnderAscii(string character, string name)
    {
        Assert.True($"a{character}b" == TerminalText.Printable($"a{character}b", TerminalCharset.Unicode), name);
        Assert.True($"a{character}b" == TerminalText.Printable($"a{character}b", TerminalCharset.Command), name);
        Assert.True("a?b" == TerminalText.Printable($"a{character}b", TerminalCharset.Ascii), name);
    }

    [Fact]
    public void PrintableAsciiIsKeptAsTheSameInstance()
    {
        string text = new([.. Enumerable.Range(0x20, 0x7f - 0x20).Select(c => (char)c)]);

        foreach (TerminalCharset charset in Enum.GetValues<TerminalCharset>())
            Assert.Same(text, TerminalText.Printable(text, charset));
    }

    [Fact]
    public void AnAstralRuneIsOneQuestionMarkUnderAscii()
    {
        // U+1F600: two UTF-16 units, one rune, one replacement. The value never grows wider.
        Assert.Equal("a?b", TerminalText.Printable("a\U0001F600b", TerminalCharset.Ascii));
    }

    [Fact]
    public void AnAstralRuneIsKeptWholeOutsideAscii()
    {
        Assert.Equal("a\U0001F600b", TerminalText.Printable("a\U0001F600b", TerminalCharset.Unicode));
        Assert.Equal("a\U0001F600b", TerminalText.Printable("a\U0001F600b", TerminalCharset.Command));
    }

    // A fact, not a theory: a lone surrogate does not survive the runner's serialisation of
    // theory data intact.
    [Fact]
    public void ALoneSurrogateIsOneQuestionMarkInEveryCharset()
    {
        (string Text, string Expected)[] cases = [("a\ud83db", "a?b"), ("a\ude00b", "a?b"), ("\ud83d", "?")];

        foreach (TerminalCharset charset in Enum.GetValues<TerminalCharset>())
        {
            foreach ((string text, string expected) in cases)
                Assert.Equal(expected, TerminalText.Printable(text, charset));
        }
    }

    [Fact]
    public void EachUnsafeRuneIsReplacedWhereItStands()
    {
        Assert.Equal(
            "Refund?[31m?Caf??Total",
            TerminalText.Printable("Refund\u001b[31m\nCaf\u00e9\u202eTotal", TerminalCharset.Ascii));
        Assert.Equal(
            "Refund?[31m?Caf\u00e9?Total",
            TerminalText.Printable("Refund\u001b[31m\nCaf\u00e9\u202eTotal", TerminalCharset.Unicode));
    }
}
