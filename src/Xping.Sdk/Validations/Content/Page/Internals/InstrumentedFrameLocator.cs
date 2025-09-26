/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xping.Sdk.Core.Common;
using Xping.Sdk.Core.Components;
using Xping.Sdk.Shared;

namespace Xping.Sdk.Validations.Content.Page.Internals;

internal class InstrumentedFrameLocator(TestContext context, IFrameLocator locator) : IFrameLocator
{
    private readonly TestContext _context = context.RequireNotNull(nameof(context));
    private readonly IFrameLocator _locator = locator.RequireNotNull(nameof(locator));

    [Obsolete("This method is obsolete in Playwright library. Use the 'ActiveLocator' property instead.")]
    public IFrameLocator First => new InstrumentedFrameLocator(_context, _locator.First);

    [Obsolete("This method is obsolete in Playwright library. Use the 'ActiveLocator' property instead.")]
    public IFrameLocator Last => new InstrumentedFrameLocator(_context, _locator.Last);

    public ILocator Owner => new InstrumentedLocator(_context, _locator.Owner);

    public IFrameLocator FrameLocator(string selector)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(FrameLocator)}.{nameof(FrameLocator)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Frame locator: {selector}"));

        var result = _locator.FrameLocator(selector);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedFrameLocator(_context, result);
    }

    public ILocator GetByAltText(string text, FrameLocatorGetByAltTextOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(FrameLocator)}.{nameof(GetByAltText)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get by alt text: {text}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameLocatorGetByAltTextOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.GetByAltText(text, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public ILocator GetByAltText(Regex text, FrameLocatorGetByAltTextOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(FrameLocator)}.{nameof(GetByAltText)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get by alt text (regex): {text}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameLocatorGetByAltTextOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.GetByAltText(text, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public ILocator GetByLabel(string text, FrameLocatorGetByLabelOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(FrameLocator)}.{nameof(GetByLabel)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get by label: {text}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameLocatorGetByLabelOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.GetByLabel(text, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public ILocator GetByLabel(Regex text, FrameLocatorGetByLabelOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(FrameLocator)}.{nameof(GetByLabel)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get by label (regex): {text}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameLocatorGetByLabelOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.GetByLabel(text, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public ILocator GetByPlaceholder(string text, FrameLocatorGetByPlaceholderOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(FrameLocator)}.{nameof(GetByPlaceholder)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get by placeholder: {text}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameLocatorGetByPlaceholderOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.GetByPlaceholder(text, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public ILocator GetByPlaceholder(Regex text, FrameLocatorGetByPlaceholderOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(FrameLocator)}.{nameof(GetByPlaceholder)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get by placeholder (regex): {text}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameLocatorGetByPlaceholderOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.GetByPlaceholder(text, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public ILocator GetByRole(AriaRole role, FrameLocatorGetByRoleOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(FrameLocator)}.{nameof(GetByRole)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get by role: {role}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameLocatorGetByRoleOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.GetByRole(role, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public ILocator GetByTestId(string testId)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(FrameLocator)}.{nameof(GetByTestId)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get by test ID: {testId}"));

        var result = _locator.GetByTestId(testId);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public ILocator GetByTestId(Regex testId)
    {
        _context.SessionBuilder
            .Build(
                 new PropertyBagKey(key: "MethodName"),
                 new PropertyBagValue<string>($"{nameof(FrameLocator)}.{nameof(GetByTestId)}"))
            .Build(
                 new PropertyBagKey(key: "DisplayName"),
                 new PropertyBagValue<string>($"Get by test ID (regex): {testId}"));

        var result = _locator.GetByTestId(testId);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public ILocator GetByText(string text, FrameLocatorGetByTextOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(FrameLocator)}.{nameof(GetByText)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get by text: {text}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameLocatorGetByTextOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.GetByText(text, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public ILocator GetByText(Regex text, FrameLocatorGetByTextOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(FrameLocator)}.{nameof(GetByText)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get by text (regex): {text}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameLocatorGetByTextOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.GetByText(text, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public ILocator GetByTitle(string text, FrameLocatorGetByTitleOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(FrameLocator)}.{nameof(GetByTitle)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get by title: {text}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameLocatorGetByTitleOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.GetByTitle(text, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public ILocator GetByTitle(Regex text, FrameLocatorGetByTitleOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(FrameLocator)}.{nameof(GetByTitle)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get by title (regex): {text}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameLocatorGetByTitleOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.GetByTitle(text, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public ILocator Locator(string selectorOrLocator, FrameLocatorLocatorOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(FrameLocator)}.{nameof(Locator)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Locator: {selectorOrLocator}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameLocatorLocatorOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.Locator(selectorOrLocator, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public ILocator Locator(ILocator selectorOrLocator, FrameLocatorLocatorOptions? options = null)
    {
        var argLocator = selectorOrLocator is InstrumentedLocator instrumented ?
            instrumented.ActiveLocator : selectorOrLocator;

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(FrameLocator)}.{nameof(Locator)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Locator: {argLocator}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameLocatorLocatorOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.Locator(selectorOrLocator, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    [Obsolete("This method is obsolete in Playwright library. Use the 'Locator' method instead.")]
    public IFrameLocator Nth(int index)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(FrameLocator)}.{nameof(Nth)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Select {index.ToOrdinal()} element"));

        var result = _locator.Nth(index);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedFrameLocator(_context, result);
    }
}
