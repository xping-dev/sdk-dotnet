/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Session.Comparison.Internals.MarkdownDecorators;

internal class ListMdDecorator(ITextReport textReport, int nestedLevel = 0) : BaseMdDecorator(textReport)
{
    private const string Indent = "  ";
    private const string ListMarker = "- ";

    private readonly int _nestedLevel = nestedLevel;

    public override string Generate()
    {
        string indentation = string.Empty;

        for (int i = 0; i < _nestedLevel; i++)
        {
            indentation += Indent;
        }

        return indentation + ListMarker + base.Generate();
    }
}
