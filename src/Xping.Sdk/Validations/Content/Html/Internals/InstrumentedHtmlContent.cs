/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Text.RegularExpressions;
using System.Xml.XPath;
using HtmlAgilityPack;
using Xping.Sdk.Core.Common;
using Xping.Sdk.Core.Components;
using Xping.Sdk.Shared;
using Xping.Sdk.Validations.Content.Html.Internals.Selectors;
using Xping.Sdk.Validations.TextUtils;

namespace Xping.Sdk.Validations.Content.Html.Internals;

internal class InstrumentedHtmlContent : IHtmlContent
{
    private readonly string _testIdAttribute;

    public TestContext Context { get; }

    public HtmlDocument Document { get; }

    public InstrumentedHtmlContent(string data, TestContext context, string testIdAttribute)
    {
        Document = new HtmlDocument();
        Document.LoadHtml(data.RequireNotNull(nameof(data)));
        Context = context.RequireNotNull(nameof(context));
        _testIdAttribute = testIdAttribute.RequireNotNullOrEmpty(nameof(testIdAttribute));
    }

    public IHtmlLocator GetByAltText(string text, TextOptions? options = null)
    {
        Context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedHtmlContent)}.{nameof(GetByAltText)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get HTML elements by alt text: {text}"))
            .Build(
                new PropertyBagKey(key: nameof(TextOptions)),
                new PropertyBagValue<string>(options?.ToString() ?? "Null"));

        var selector = CreateByAltTextSelector(text, options);
        var nodes = selector.Select(Document.DocumentNode) ?? throw new ValidationException(
            $"No nodes were found using the locator. Ensure that the HTML content has the appropriate nodes " +
            $"before attempting to perform the selection.");

        // Create a successful test step with detailed information about the current state of the HTML locator.
        var testStep = Context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Retrieved {nodes.Count} node(s)"))
            .Build();
        // Report the progress of this test step.
        Context.Progress?.Report(testStep);

        return new InstrumentedHtmlLocator(nodes, new HtmlNodeIterator(nodes), Context);
    }

    public IHtmlLocator GetByAltText(Regex text, TextOptions? options = null)
    {
        Context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedHtmlContent)}.{nameof(GetByAltText)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get HTML elements by alt text (regex): {text}"))
            .Build(
                new PropertyBagKey(key: nameof(TextOptions)),
                new PropertyBagValue<string>(options?.ToString() ?? "Null"));

        var selector = CreateByAltRegexSelector(text, options);
        var nodes = selector.Select(Document.DocumentNode) ?? throw new ValidationException(
            $"No nodes were found using the locator. Ensure that the HTML content has the appropriate nodes " +
            $"before attempting to perform the selection.");

        // Create a successful test step with detailed information about the current state of the HTML locator.
        var testStep = Context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Retrieved {nodes.Count} node(s)"))
            .Build();
        
        // Report the progress of this test step.
        Context.Progress?.Report(testStep);

        return new InstrumentedHtmlLocator(nodes, new HtmlNodeIterator(nodes), Context);
    }

    public IHtmlLocator GetByLabel(string text, TextOptions? options = null)
    {
        Context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedHtmlContent)}.{nameof(GetByLabel)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get HTML elements by label text: {text}"))
            .Build(
                new PropertyBagKey(key: nameof(TextOptions)),
                new PropertyBagValue<string>(options?.ToString() ?? "Null"));

        var selector = CreateByLabelTextSelector(text);
        var nodes = selector.Select(Document.DocumentNode) ?? throw new ValidationException(
            $"No nodes were found using the locator. Ensure that the HTML content has the appropriate nodes " +
            $"before attempting to perform the selection.");

        // Create a successful test step with detailed information about the current state of the HTML locator.
        var testStep = Context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Retrieved {nodes.Count} node(s)"))
            .Build();
        // Report the progress of this test step.
        Context.Progress?.Report(testStep);

        return new InstrumentedHtmlLocator(nodes, new HtmlNodeIterator(nodes), Context);
    }

    public IHtmlLocator GetByLabel(Regex text, TextOptions? options = null)
    {
        Context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedHtmlContent)}.{nameof(GetByLabel)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get HTML elements by label text (regex): {text}"))
            .Build(
                new PropertyBagKey(key: nameof(TextOptions)),
                new PropertyBagValue<string>(options?.ToString() ?? "Null"));

        var selector = CreateByLabelRegexSelector(text, options);
        var nodes = selector.Select(Document.DocumentNode) ?? throw new ValidationException(
            $"No nodes were found using the locator. Ensure that the HTML content has the appropriate nodes " +
            $"before attempting to perform the selection.");

        // Create a successful test step with detailed information about the current state of the HTML locator.
        var testStep = Context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Retrieved {nodes.Count} node(s)"))
            .Build();
        // Report the progress of this test step.
        Context.Progress?.Report(testStep);

        return new InstrumentedHtmlLocator(nodes, new HtmlNodeIterator(nodes), Context);
    }

    public IHtmlLocator GetByPlaceholder(string text, TextOptions? options = null)
    {
        Context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedHtmlContent)}.{nameof(GetByPlaceholder)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get HTML elements by placeholder text: {text}"))
            .Build(
                new PropertyBagKey(key: nameof(TextOptions)),
                new PropertyBagValue<string>(options?.ToString() ?? "Null"));

        var selector = CreateByPlaceholderTextSelector(text, options);
        var nodes = selector.Select(Document.DocumentNode) ?? throw new ValidationException(
            $"No nodes were found using the locator. Ensure that the HTML content has the appropriate nodes " +
            $"before attempting to perform the selection.");

        // Create a successful test step with detailed information about the current state of the HTML locator.
        var testStep = Context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Retrieved {nodes.Count} node(s)"))
            .Build();
        // Report the progress of this test step.
        Context.Progress?.Report(testStep);

        return new InstrumentedHtmlLocator(nodes, new HtmlNodeIterator(nodes), Context);
    }

    public IHtmlLocator GetByPlaceholder(Regex text, TextOptions? options = null)
    {
        Context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedHtmlContent)}.{nameof(GetByPlaceholder)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get HTML elements by placeholder text (regex): {text}"))
            .Build(
                new PropertyBagKey(key: nameof(TextOptions)),
                new PropertyBagValue<string>(options?.ToString() ?? "Null"));

        var selector = CreateByPlaceholderRegexSelector(text, options);
        var nodes = selector.Select(Document.DocumentNode) ?? throw new ValidationException(
            $"No nodes were found using the locator. Ensure that the HTML content has the appropriate nodes " +
            $"before attempting to perform the selection.");

        // Create a successful test step with detailed information about the current state of the HTML locator.
        var testStep = Context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Retrieved {nodes.Count} node(s)"))
            .Build();
        // Report the progress of this test step.
        Context.Progress?.Report(testStep);

        return new InstrumentedHtmlLocator(nodes, new HtmlNodeIterator(nodes), Context);
    }

    public IHtmlLocator GetByTestId(string text, TextOptions? options = null)
    {
        Context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedHtmlContent)}.{nameof(GetByTestId)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get HTML elements by test ID: {text}"))
            .Build(
                new PropertyBagKey(key: nameof(TextOptions)),
                new PropertyBagValue<string>(options?.ToString() ?? "Null"));

        var selector = CreateByTestIdTextSelector(text, options);
        var nodes = selector.Select(Document.DocumentNode) ?? throw new ValidationException(
            $"No nodes were found using the locator. Ensure that the HTML content has the appropriate nodes " +
            $"before attempting to perform the selection.");

        // Create a successful test step with detailed information about the current state of the HTML locator.
        var testStep = Context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Retrieved {nodes.Count} node(s)"))
            .Build();
        // Report the progress of this test step.
        Context.Progress?.Report(testStep);

        return new InstrumentedHtmlLocator(nodes, new HtmlNodeIterator(nodes), Context);
    }

    public IHtmlLocator GetByTestId(Regex text, TextOptions? options = null)
    {
        Context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedHtmlContent)}.{nameof(GetByTestId)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get HTML elements by test ID (regex): {text}"))
            .Build(
                new PropertyBagKey(key: nameof(TextOptions)),
                new PropertyBagValue<string>(options?.ToString() ?? "Null"));

        var selector = CreateByTestIdRegexSelector(text, options);
        var nodes = selector.Select(Document.DocumentNode) ?? throw new ValidationException(
            $"No nodes were found using the locator. Ensure that the HTML content has the appropriate nodes " +
            $"before attempting to perform the selection.");

        // Create a successful test step with detailed information about the current state of the HTML locator.
        var testStep = Context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Retrieved {nodes.Count} node(s)"))
            .Build();
        // Report the progress of this test step.
        Context.Progress?.Report(testStep);

        return new InstrumentedHtmlLocator(nodes, new HtmlNodeIterator(nodes), Context);
    }

    public IHtmlLocator GetByTitle(string text, TextOptions? options = null)
    {
        Context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedHtmlContent)}.{nameof(GetByTitle)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get HTML elements by title attribute: {text}"))
            .Build(
                new PropertyBagKey(key: nameof(TextOptions)),
                new PropertyBagValue<string>(options?.ToString() ?? "Null"));

        var selector = CreateByTitleTextSelector(text, options);
        var nodes = selector.Select(Document.DocumentNode) ?? throw new ValidationException(
            $"No nodes were found using the locator. Ensure that the HTML content has the appropriate nodes " +
            $"before attempting to perform the selection.");

        // Create a successful test step with detailed information about the current state of the HTML locator.
        var testStep = Context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Retrieved {nodes.Count} node(s)"))
            .Build();
        // Report the progress of this test step.
        Context.Progress?.Report(testStep);

        return new InstrumentedHtmlLocator(nodes, new HtmlNodeIterator(nodes), Context);
    }

    public IHtmlLocator GetByTitle(Regex text, TextOptions? options = null)
    {
        Context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedHtmlContent)}.{nameof(GetByTitle)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get HTML elements by title attribute (regex): {text}"))
            .Build(
                new PropertyBagKey(key: nameof(TextOptions)),
                new PropertyBagValue<string>(options?.ToString() ?? "Null"));

        var selector = CreateByTitleRegexSelector(text, options);
        var nodes = selector.Select(Document.DocumentNode) ?? throw new ValidationException(
            $"No nodes were found using the locator. Ensure that the HTML content has the appropriate nodes " +
            $"before attempting to perform the selection.");

        // Create a successful test step with detailed information about the current state of the HTML locator.
        var testStep = Context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Retrieved {nodes.Count} node(s)"))
            .Build();
        // Report the progress of this test step.
        Context.Progress?.Report(testStep);

        return new InstrumentedHtmlLocator(nodes, new HtmlNodeIterator(nodes), Context);
    }

    public IHtmlLocator Locator(XPathExpression selector, FilterOptions? options = null)
    {
        Context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedHtmlContent)}.{nameof(Locator)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get HTML elements by XPath expression: {selector.Expression}"))
            .Build(
                new PropertyBagKey(key: nameof(FilterOptions)),
                new PropertyBagValue<string>(options?.ToString() ?? "Null"));

        XPathSelector xpathSelector = new(selector);
        var nodes = xpathSelector.Select(Document.DocumentNode) ?? throw new ValidationException(
            $"No nodes were found using the locator. Ensure that the HTML content has the appropriate nodes " +
            $"before attempting to perform the selection.");

        // Create a successful test step with detailed information about the current state of the HTML locator.
        var testStep = Context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Retrieved {nodes.Count} node(s)"))
            .Build();
        // Report the progress of this test step.
        Context.Progress?.Report(testStep);

        return new InstrumentedHtmlLocator(nodes, new HtmlNodeIterator(nodes), Context);
    }
    
    public IHtmlLocator Locator(string selector, FilterOptions? options = null)
    {
        return Locator(XPathExpression.Compile(selector), options);
    }

    protected virtual ISelector CreateByAltTextSelector(string text, TextOptions? options = null)
    {
        return new AttributeTextSelector(XPaths.Alt, text, options);
    }

    protected virtual ISelector CreateByAltRegexSelector(Regex text, TextOptions? options = null)
    {
        return new AttributeRegexSelector(XPaths.Alt, text, options);
    }

    protected virtual ISelector CreateByLabelTextSelector(string text, TextOptions? options = null)
    {
        return new NodeTextSelector(XPaths.Label, text, options);
    }

    protected virtual ISelector CreateByLabelRegexSelector(Regex text, TextOptions? options = null)
    {
        return new NodeRegexSelector(XPaths.Label, text, options);
    }

    protected virtual ISelector CreateByPlaceholderTextSelector(string text, TextOptions? options = null)
    {
        return new AttributeTextSelector(XPaths.Placeholder, text, options);
    }

    protected virtual ISelector CreateByPlaceholderRegexSelector(Regex text, TextOptions? options = null)
    {
        return new AttributeRegexSelector(XPaths.Placeholder, text, options);
    }

    protected virtual ISelector CreateByTestIdTextSelector(string text, TextOptions? options = null)
    {
        return new AttributeTextSelector(XPaths.TestIdAttribute(_testIdAttribute), text, options);
    }

    protected virtual ISelector CreateByTestIdRegexSelector(Regex text, TextOptions? options = null)
    {
        return new AttributeRegexSelector(XPaths.TestIdAttribute(_testIdAttribute), text, options);
    }

    protected virtual ISelector CreateByTitleTextSelector(string text, TextOptions? options = null)
    {
        return new AttributeTextSelector(XPaths.TitleAttribute, text, options);
    }

    protected virtual ISelector CreateByTitleRegexSelector(Regex text, TextOptions? options = null)
    {
        return new AttributeRegexSelector(XPaths.TitleAttribute, text, options);
    }
}
