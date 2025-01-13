/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Session.Comparison.Internals.MarkdownDecorators;

internal class TableRowEndMdDecorator(ITextReport textReport) : BaseMdDecorator(textReport)
{
    private const string TableRowEndMark = "|\r\n";

    public override string Generate()
    {
        return base.Generate() + TableRowEndMark;
    }
}
