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
                new PropertyBagValue<string>(nameof(First)))
            .Build(
                new PropertyBagKey(key: "Nodes"),
                new PropertyBagValue<string[]>(Nodes.Select(n => n.OriginalName.Trim()).ToArray()));

        Iterator.First();

        // Create a successful test step with detailed information about the current state of the HTML locator.
        var testStep = Context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "CurrentNode"),
                new PropertyBagValue<string>(Iterator.Current()?.OriginalName.Trim() ?? "Null"))
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
                new PropertyBagValue<string>(nameof(Last)))
            .Build(
                new PropertyBagKey(key: "Nodes"),
                new PropertyBagValue<string[]>(Nodes.Select(n => n.OriginalName.Trim()).ToArray()));

        Iterator.Last();

        // Create a successful test step with detailed information about the current state of the HTML locator.
        var testStep = Context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "CurrentNode"),
                new PropertyBagValue<string>(Iterator.Current()?.OriginalName.Trim() ?? "Null"))
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
                new PropertyBagValue<string>(nameof(Filter)))
            .Build(
                new PropertyBagKey(key: "CurrentNode"),
                new PropertyBagValue<string>(currentNode?.OriginalName.Trim() ?? "Null"))
            .Build(
                new PropertyBagKey(key: nameof(FilterOptions)),
                new PropertyBagValue<string>(filter.ToString()))
            .Build(
                new PropertyBagKey(key: nameof(TextOptions)),
                new PropertyBagValue<string>(options?.ToString() ?? "Null"));

        HtmlNodeCollection? filteredNodes = null;

        if (currentNode != null)
        {
            Context.SessionBuilder.Build(
                new PropertyBagKey(key: "ChildNodes"),
                new PropertyBagValue<string[]>(
                    currentNode.ChildNodes.Select(n => n.OriginalName.Trim()).ToArray()));

            FilterSelector filterSelector = new(filter, options);
            filteredNodes = filterSelector.Select(currentNode);

            Context.SessionBuilder.Build(
                new PropertyBagKey(key: "FilteredNodes"),
                new PropertyBagValue<string[]>(filteredNodes.Select(n => n.OriginalName.Trim()).ToArray()));
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
                new PropertyBagValue<string>(nameof(Locate)))
            .Build(
                new PropertyBagKey(key: "CurrentNode"),
                new PropertyBagValue<string>(currentNode?.OriginalName.Trim() ?? "Null"))
            .Build(
                new PropertyBagKey(key: nameof(selector)),
                new PropertyBagValue<string>(selector.Expression));

        HtmlNodeCollection? locatedNodes = null;

        if (currentNode != null)
        {
            Context.SessionBuilder.Build(
                new PropertyBagKey(key: "ChildNodes"),
                new PropertyBagValue<string[]>(
                    currentNode.ChildNodes.Select(n => n.OriginalName.Trim()).ToArray()));

            XPathSelector xpathSelector = new(selector);
            locatedNodes = xpathSelector.Select(currentNode);

            Context.SessionBuilder
                .Build(
                    new PropertyBagKey(key: "LocatedNodes"),
                    new PropertyBagValue<string[]>(locatedNodes?.Select(n => n.OriginalName.Trim()).ToArray() ?? ["Null"]));
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
                new PropertyBagValue<string>(nameof(Nth)))
            .Build(
                new PropertyBagKey(key: nameof(index)),
                new PropertyBagValue<string>(index.ToString(CultureInfo.InvariantCulture)))
            .Build(
                new PropertyBagKey(key: "Nodes"),
                new PropertyBagValue<string[]>(Nodes.Select(n => n.OriginalName.Trim()).ToArray()));

        if (index >= Nodes.Count)
        {
            throw new ValidationException(
                $"Expected to access the {FormatIndex(index)} index, but only {Nodes.Count} elements exist." +
                $" This error occurred during the validation of HTML data.");
        }

        Iterator.Nth(index);

        // Create a successful test step with detailed information about the current state of the HTML locator.
        var testStep = Context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "CurrentNode"),
                new PropertyBagValue<string>(Iterator.Current()?.OriginalName.Trim() ?? "Null"))
            .Build();
        // Report the progress of this test step.
        Context.Progress?.Report(testStep);

        return this;

        static string FormatIndex(int index)
        {
            var suffix = (index % 10) switch
            {
                1 when index % 100 != 11 => "st",
                2 when index % 100 != 12 => "nd",
                3 when index % 100 != 13 => "rd",
                _ => "th",
            };
            return $"{index}{suffix}";
        }
    }
}
