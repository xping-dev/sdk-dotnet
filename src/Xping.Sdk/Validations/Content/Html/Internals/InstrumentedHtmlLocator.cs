/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Globalization;
using System.Xml.XPath;
using HtmlAgilityPack;
using Xping.Sdk.Core.Common;
using Xping.Sdk.Core.Components;
using Xping.Sdk.Shared;
using Xping.Sdk.Validations.Content.Html.Internals.Selectors;
using Xping.Sdk.Validations.TextUtils;

namespace Xping.Sdk.Validations.Content.Html.Internals;

internal class InstrumentedHtmlLocator : IHtmlLocator
{
    public HtmlNodeCollection Nodes { get; }
    public IIterator<HtmlNode> Iterator { get; }
    public TestContext Context { get; }

    public InstrumentedHtmlLocator(
        HtmlNodeCollection nodes,
        IIterator<HtmlNode> iterator,
        TestContext context)
    {
        Nodes = nodes.RequireNotNull(nameof(nodes));
        Iterator = iterator.RequireNotNull(nameof(iterator));
        Context = context.RequireNotNull(nameof(context));

        // If the collection of nodes has elements, advance the iterator to the first item. This allows validation
        // functions like ToHaveInnerText to be called without needing to advance the iterator first.
        if (Nodes.Count >= 1)
        {
            Iterator.First();
        }
    }

    public IHtmlLocator First()
    {
        Context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedHtmlLocator)}.{nameof(First)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Advance to the first HTML node"));

        Iterator.First();

        var currentName = (Iterator.Current()?.OriginalName.Trim())
            ?? throw new ValidationException("Expected to access the first element, but the collection is empty.");

        // Create a successful test step with detailed information about the current state of the HTML locator.
        var testStep = Context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"First element: {currentName}"))
            .Build();
        // Report the progress of this test step.
        Context.Progress?.Report(testStep);

        return this;
    }

    public IHtmlLocator Last()
    {
        Context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedHtmlLocator)}.{nameof(Last)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Advance to the last HTML node"));

        Iterator.Last();

        var currentName = (Iterator.Current()?.OriginalName.Trim())
            ?? throw new ValidationException("Expected to access the last element, but the collection is empty.");

        // Create a successful test step with detailed information about the current state of the HTML locator.
        var testStep = Context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Last element: {currentName}"))
            .Build();
        // Report the progress of this test step.
        Context.Progress?.Report(testStep);

        return this;
    }

    public IHtmlLocator Filter(FilterOptions filter, TextOptions? options = null)
    {
        var currentNode = Iterator.Current();

        Context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedHtmlLocator)}.{nameof(Filter)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Filter HTML nodes"))
            .Build(
                new PropertyBagKey(key: nameof(FilterOptions)),
                new PropertyBagValue<string>(filter.ToString()))
            .Build(
                new PropertyBagKey(key: nameof(TextOptions)),
                new PropertyBagValue<string>(options?.ToString() ?? "Null"));

        HtmlNodeCollection? filteredNodes = null;

        if (currentNode != null)
        {
            FilterSelector filterSelector = new(filter, options);
            filteredNodes = filterSelector.Select(currentNode);

            Context.SessionBuilder.Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>(
                    $"Filtered nodes: {string.Join(", ", filteredNodes.Select(n => n.OriginalName.Trim()))}"));
        }
        else
        {
            Context.SessionBuilder.Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>("Filtered nodes: Null"));
        }

        // Create a successful test step with detailed information about the current state of the HTML locator.
        var testStep = Context.SessionBuilder.Build();
        // Report the progress of this test step.
        Context.Progress?.Report(testStep);

        return filteredNodes is { Count: > 0 }
            ? new InstrumentedHtmlLocator(filteredNodes, new HtmlNodeIterator(filteredNodes), Context)
            : this;
    }

    public IHtmlLocator Locate(XPathExpression selector)
    {
        var currentNode = Iterator.Current();

        Context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedHtmlLocator)}.{nameof(Locate)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Locate HTML nodes using selector: {selector.Expression}"));

        HtmlNodeCollection? locatedNodes = null;

        if (currentNode != null)
        {
            XPathSelector xpathSelector = new(selector);
            locatedNodes = xpathSelector.Select(currentNode);

            if (locatedNodes == null || locatedNodes.Count == 0)
            {
                throw new ValidationException(
                    $"Expected to locate nodes using the selector '{selector.Expression}', " +
                    $"but no elements were found.");
            }

            Context.SessionBuilder
                .Build(
                    new PropertyBagKey(key: "Result"),
                    new PropertyBagValue<string>(
                        $"Located nodes: [{string.Join(", ", locatedNodes.Select(n => n.OriginalName.Trim()))}]"));
        }
        else
        {
            Context.SessionBuilder.Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>("Located nodes: [Null]"));
        }

        // Create a successful test step with detailed information about the current state of the HTML locator.
        var testStep = Context.SessionBuilder.Build();
        // Report the progress of this test step.
        Context.Progress?.Report(testStep);

        return locatedNodes != null
            ? new InstrumentedHtmlLocator(locatedNodes, new HtmlNodeIterator(locatedNodes), Context)
            : this;
    }

    public IHtmlLocator Nth(int index)
    {
        if (index < 0)
        {
            throw new ArgumentException("Index must be a positive integer.");
        }

        Context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedHtmlLocator)}.{nameof(Nth)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Advance to the {index.ToOrdinal()} HTML node"));

        if (Nodes.Count == 0)
        {
            throw new ValidationException(
                $"Expected to access the {index.ToOrdinal()} index, but the collection is empty.");
        }
        else if (index >= Nodes.Count)
        {
            throw new ValidationException(
                $"Expected to access the {index.ToOrdinal()} index, but only {Nodes.Count} elements exist.");
        }

        Iterator.Nth(index);

        var currentName = (Iterator.Current()?.OriginalName.Trim())
            ?? throw new ValidationException("Expected to access the specified element, but the collection is empty.");

        // Create a successful test step with detailed information about the current state of the HTML locator.
        var testStep = Context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Located the {index.ToOrdinal()} node: {currentName}"))
            .Build();
        // Report the progress of this test step.
        Context.Progress?.Report(testStep);

        return this;
    }
}
