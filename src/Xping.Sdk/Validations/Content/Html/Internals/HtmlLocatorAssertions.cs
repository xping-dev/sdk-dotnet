using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Xping.Sdk.Core.Common;
using Xping.Sdk.Core.Components;
using Xping.Sdk.Validations.TextUtils;
using Xping.Sdk.Validations.TextUtils.Internals;

namespace Xping.Sdk.Validations.Content.Html.Internals;

internal class HtmlLocatorAssertions(IHtmlLocator htmlLocator) : IHtmlLocatorAssertions
{
    private readonly HtmlNodeCollection _nodes = htmlLocator.Nodes;
    private readonly IIterator<HtmlNode> _iterator = htmlLocator.Iterator;
    private readonly TestContext _context = htmlLocator.Context;

    public IHtmlLocatorAssertions ToHaveCount(int expectedCount)
    {
        var actualCount = _nodes.Count;

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToHaveCount)))
            .Build(
                new PropertyBagKey(key: nameof(expectedCount)),
                new PropertyBagValue<string>($"{expectedCount}"))
            .Build(
                new PropertyBagKey(key: "Nodes"),
                new PropertyBagValue<string[]>(_nodes.Select(n => n.OriginalName.Trim()).ToArray()));

        if (actualCount != expectedCount)
        {
            throw new ValidationException(
                $"Expected to find {expectedCount} elements, but found {actualCount} instead. This error " +
                $"occurred as part of validating HTML data.");
        }

        // Create a successful test step with detailed information about the current state of the HTML locator.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return this;
    }

    public IHtmlLocatorAssertions ToHaveInnerText(string innerText, TextOptions? options = default)
    {
        var currentNode = _iterator.Current();
        var textComparer = new TextComparer(options);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToHaveInnerText)))
            .Build(
                new PropertyBagKey(key: nameof(innerText)),
                new PropertyBagValue<string>(innerText))
            .Build(
                new PropertyBagKey(key: nameof(TextOptions)),
                new PropertyBagValue<string>(options?.ToString() ?? "Null"))
            .Build(
                new PropertyBagKey(key: "CurrentNode"),
                new PropertyBagValue<string>(currentNode?.OriginalName.Trim() ?? "Null"));

        if (currentNode == null || _nodes.Count == 0)
        {
            throw new ValidationException(
                "No HTML nodes available. Ensure that the locator has selected at least one node before attempting " +
                "to validate inner text. This error occurred during the validation of HTML data.");
        }

        var actualText = currentNode.InnerText.Trim();

        if (!textComparer.Compare(actualText, innerText))
        {
            throw new ValidationException(
                $"Expected the HTML node's inner text to be \"{innerText}\", but the actual inner text was " +
                $"\"{actualText}\". This error occurred during the validation of HTML data.");
        }

        // Create a successful test step with detailed information about the current state of the HTML locator.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return this;
    }

    public IHtmlLocatorAssertions ToHaveInnerText(Regex innerText, TextOptions? options = null)
    {
        var currentNode = _iterator.Current();
        var textComparer = new TextComparer(options);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToHaveInnerText)))
            .Build(
                new PropertyBagKey(key: nameof(innerText)),
                new PropertyBagValue<string>(innerText.ToString()))
            .Build(
                new PropertyBagKey(key: nameof(TextOptions)),
                new PropertyBagValue<string>(options?.ToString() ?? "Null"))
            .Build(
                new PropertyBagKey(key: "CurrentNode"),
                new PropertyBagValue<string>(currentNode?.OriginalName.Trim() ?? "Null"));

        if (currentNode == null || _nodes.Count == 0)
        {
            throw new ValidationException(
                "No HTML nodes available. Ensure that the locator has selected at least one node before attempting " +
                "to validate inner text. This error occurred during the validation of HTML data.");
        }

        var actualText = currentNode.InnerText.Trim();

        if (!textComparer.CompareWithRegex(actualText, innerText.ToString()))
        {
            throw new ValidationException(
                $"Expected the HTML node's inner text to match \"{innerText}\" regex, but the actual inner text was " +
                $"\"{actualText}\". This error occurred during the validation of HTML data.");
        }

        // Create a successful test step with detailed information about the current state of the HTML locator.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return this;
    }

    public IHtmlLocatorAssertions ToHaveInnerText(IEnumerable<string> nodeInnerTexts, TextOptions? options = null)
    {
        var currentNode = _iterator.Current();
        var textComparer = new TextComparer(options);
        var innerTexts = nodeInnerTexts as string[] ?? nodeInnerTexts.ToArray();

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToHaveInnerText)))
            .Build(
                new PropertyBagKey(key: nameof(nodeInnerTexts)),
                new PropertyBagValue<string[]>(innerTexts))
            .Build(
                new PropertyBagKey(key: nameof(TextOptions)),
                new PropertyBagValue<string>(options?.ToString() ?? "Null"))
            .Build(
                new PropertyBagKey(key: "CurrentNode"),
                new PropertyBagValue<string>(currentNode?.OriginalName.Trim() ?? "Null"));

        if (currentNode == null || _nodes.Count == 0)
        {
            throw new ValidationException(
                "No HTML nodes available. Ensure that the locator has selected at least one node before attempting " +
                "to validate inner text. This error occurred during the validation of HTML data.");
        }

        // Check if the lengths of the arrays are different
        if (innerTexts.Length != _iterator.Count)
        {
            throw new ValidationException(
                "The number of inner text values provided does not match the number of HTML nodes located. " +
                "Ensure that each node corresponds to a unique inner text value. This error occurred during the " +
                "validation of HTML data.");
        }

        for (int i = 0; i < _iterator.Count; i++)
        {
            _iterator.Nth(i);
            var actualText = _iterator.Current()?.InnerText.Trim();
            var expectedText = innerTexts[i].Trim();

            if (!textComparer.Compare(actualText, expectedText))
            {
                throw new ValidationException(
                    $"Expected the HTML node's inner text to match \"{expectedText}\" regex, but the actual inner " +
                    $"text was \"{actualText}\". This error occurred during the validation of HTML data.");
            }
        }

        // Create a successful test step with detailed information about the current state of the HTML locator.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return this;
    }

    public IHtmlLocatorAssertions ToHaveInnerHtml(string innerHtml, TextOptions? options = null)
    {
        var currentNode = _iterator.Current();
        var textComparer = new TextComparer(options);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToHaveInnerHtml)))
            .Build(
                new PropertyBagKey(key: nameof(innerHtml)),
                new PropertyBagValue<string>(innerHtml))
            .Build(
                new PropertyBagKey(key: nameof(TextOptions)),
                new PropertyBagValue<string>(options?.ToString() ?? "Default"))
            .Build(
                new PropertyBagKey(key: "CurrentNode"),
                new PropertyBagValue<string>(currentNode?.OriginalName.Trim() ?? "Null"));

        if (currentNode == null || _nodes.Count == 0)
        {
            throw new ValidationException(
                "No HTML nodes available. Ensure that the locator has selected at least one node before attempting " +
                "to validate inner text. This error occurred during the validation of HTML data.");
        }

        var actualHtml = currentNode.InnerHtml.Trim();

        if (!textComparer.Compare(actualHtml, innerHtml))
        {
            throw new ValidationException(
                $"Expected the HTML node's inner html to be \"{innerHtml}\", but the actual inner html was " +
                $"\"{actualHtml}\". This error occurred during the validation of HTML data.");
        }

        // Create a successful test step with detailed information about the current state of the HTML locator.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return this;
    }

    public IHtmlLocatorAssertions ToHaveInnerHtml(Regex innerHtml, TextOptions? options = null)
    {
        var currentNode = _iterator.Current();
        var textComparer = new TextComparer(options);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToHaveInnerHtml)))
            .Build(
                new PropertyBagKey(key: nameof(innerHtml)),
                new PropertyBagValue<string>(innerHtml.ToString()))
            .Build(
                new PropertyBagKey(key: nameof(TextOptions)),
                new PropertyBagValue<string>(options?.ToString() ?? "Null"))
            .Build(
                new PropertyBagKey(key: "CurrentNode"),
                new PropertyBagValue<string>(currentNode?.OriginalName.Trim() ?? "Null"));

        if (currentNode == null || _nodes.Count == 0)
        {
            throw new ValidationException(
                "No HTML nodes available. Ensure that the locator has selected at least one node before attempting " +
                "to validate inner text. This error occurred during the validation of HTML data.");
        }

        var actualHtml = currentNode.InnerHtml.Trim();

        if (!textComparer.CompareWithRegex(actualHtml, innerHtml.ToString()))
        {
            throw new ValidationException(
                $"Expected the HTML node's inner html to match \"{innerHtml}\" regex, but the actual inner html was " +
                $"\"{actualHtml}\". This error occurred during the validation of HTML data.");
        }

        // Create a successful test step with detailed information about the current state of the HTML locator.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return this;
    }
}