/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Session.Comparison.Internals.MarkdownDecorators;

internal class HeaderMdDecorator(ITextReport textReport, HeaderType header) : BaseMdDecorator(textReport)
{
    private const string H1 = "# ";
    private const string H2 = "## ";
    private const string H3 = "### ";
    private const string H4 = "#### ";

    private readonly HeaderType _header = header;

    public override string Generate()
    {
        var header = _header switch
        {
            HeaderType.H1 => H1,
            HeaderType.H2 => H2,
            HeaderType.H3 => H3,
            HeaderType.H4 => H4,
            _ => string.Empty // In case we did not support provided header, we skip it entirely.
        };

        return header + base.Generate() + Environment.NewLine;
    }
}
