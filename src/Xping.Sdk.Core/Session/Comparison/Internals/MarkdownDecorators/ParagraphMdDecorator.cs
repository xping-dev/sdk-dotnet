/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Session.Comparison.Internals.MarkdownDecorators;

internal class ParagraphMdDecorator(ITextReport textReport) : BaseMdDecorator(textReport)
{
    public override string Generate()
    {
        return base.Generate() + Environment.NewLine;
    }
}
