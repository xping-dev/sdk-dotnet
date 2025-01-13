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
                new PropertyBagValue<string>(nameof(FrameLocator)))
            .Build(
                new PropertyBagKey(key: nameof(selector)),
                new PropertyBagValue<string>(selector));

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
                new PropertyBagValue<string>(nameof(GetByAltText)))
            .Build(
                new PropertyBagKey(key: nameof(text)),
                new PropertyBagValue<string>(text))
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
                new PropertyBagValue<string>(nameof(GetByAltText)))
            .Build(
                new PropertyBagKey(key: nameof(text)),
                new PropertyBagValue<string>(text.ToString()))
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
                new PropertyBagValue<string>(nameof(GetByLabel)))
            .Build(
                new PropertyBagKey(key: nameof(text)),
                new PropertyBagValue<string>(text))
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
                new PropertyBagValue<string>(nameof(GetByLabel)))
            .Build(
                new PropertyBagKey(key: nameof(text)),
                new PropertyBagValue<string>(text.ToString()))
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
                new PropertyBagValue<string>(nameof(GetByPlaceholder)))
            .Build(
                new PropertyBagKey(key: nameof(text)),
                new PropertyBagValue<string>(text))
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
                new PropertyBagValue<string>(nameof(GetByPlaceholder)))
            .Build(
                new PropertyBagKey(key: nameof(text)),
                new PropertyBagValue<string>(text.ToString()))
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
                new PropertyBagValue<string>(nameof(GetByRole)))
            .Build(
                new PropertyBagKey(key: nameof(role)),
                new PropertyBagValue<string>(role.ToString()))
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
                new PropertyBagValue<string>(nameof(GetByTestId)))
            .Build(
                new PropertyBagKey(key: nameof(testId)),
                new PropertyBagValue<string>(testId));

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
                 new PropertyBagValue<string>(nameof(GetByTestId)))
            .Build(
                 new PropertyBagKey(key: nameof(testId)),
                 new PropertyBagValue<string>(testId.ToString()));

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
                new PropertyBagValue<string>(nameof(GetByText)))
            .Build(
                new PropertyBagKey(key: nameof(text)),
                new PropertyBagValue<string>(text))
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
                new PropertyBagValue<string>(nameof(GetByText)))
            .Build(
                new PropertyBagKey(key: nameof(text)),
                new PropertyBagValue<string>(text.ToString()))
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
                new PropertyBagValue<string>(nameof(GetByTitle)))
            .Build(
                new PropertyBagKey(key: nameof(text)),
                new PropertyBagValue<string>(text))
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
                new PropertyBagValue<string>(nameof(GetByTitle)))
            .Build(
                new PropertyBagKey(key: nameof(text)),
                new PropertyBagValue<string>(text.ToString()))
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
                new PropertyBagValue<string>(nameof(Locator)))
            .Build(
                new PropertyBagKey(key: nameof(selectorOrLocator)),
                new PropertyBagValue<string>(selectorOrLocator))
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
                new PropertyBagValue<string>(nameof(Locator)))
            .Build(
                new PropertyBagKey(key: nameof(selectorOrLocator)),
                new PropertyBagValue<string>(argLocator.ToString() ?? "Null"))
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
                new PropertyBagValue<string>(nameof(Nth)))
            .Build(
                new PropertyBagKey(key: nameof(index)),
                new PropertyBagValue<string>(index.ToString(CultureInfo.InvariantCulture)));

        var result = _locator.Nth(index);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedFrameLocator(_context, result);
    }
}
