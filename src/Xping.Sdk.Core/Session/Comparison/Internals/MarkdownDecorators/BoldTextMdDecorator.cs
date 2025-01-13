/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Session.Comparison.Internals.MarkdownDecorators;

internal class BoldTextMdDecorator(ITextReport textReport) : BaseMdDecorator(textReport)
{
    private const string BoldMarker = "**";

    public override string Generate()
    {
        var text = base.Generate();

        if (!string.IsNullOrEmpty(text))
        {
            return BoldMarker + base.Generate() + BoldMarker;
        }

        return text;        
    }
}
