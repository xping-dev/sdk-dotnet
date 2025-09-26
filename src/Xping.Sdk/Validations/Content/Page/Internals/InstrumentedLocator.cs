/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xping.Sdk.Core.Common;
using Xping.Sdk.Core.Components;
using Xping.Sdk.Shared;

namespace Xping.Sdk.Validations.Content.Page.Internals;

internal class InstrumentedLocator(TestContext context, ILocator locator) : ILocator
{
    private readonly TestContext _context = context.RequireNotNull(nameof(context));
    private readonly ILocator _locator = locator.RequireNotNull(nameof(locator));

    internal TestContext GetTestContext() => _context;
    
    public ILocator ActiveLocator => _locator;

    public ILocator First => new InstrumentedLocator(_context, _locator.First);

    public ILocator Last => new InstrumentedLocator(_context, _locator.Last);

    public IPage Page => new InstrumentedPage(_context, _locator.Page);

    public IFrameLocator ContentFrame => new InstrumentedFrameLocator(_context, _locator.ContentFrame);

    public async Task<IReadOnlyList<ILocator>> AllAsync()
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(AllAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Retrieving all matching locators"));

        var result = await _locator.AllAsync().ConfigureAwait(false);

        _context.SessionBuilder
           .Build(
               new PropertyBagKey(key: "Result"),
               new PropertyBagValue<string>($"Found {result.Count} matching locators"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result.Select(l => new InstrumentedLocator(_context, l)).ToList().AsReadOnly();
    }

    public async Task<IReadOnlyList<string>> AllInnerTextsAsync()
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(AllInnerTextsAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Retrieving all inner texts"));

        var result = await _locator.AllInnerTextsAsync().ConfigureAwait(false);

        _context.SessionBuilder
           .Build(
               new PropertyBagKey(key: "Result"),
               new PropertyBagValue<string>($"Found {result.Count} inner texts"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<IReadOnlyList<string>> AllTextContentsAsync()
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(AllTextContentsAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Retrieving all text contents"));

        var result = await _locator.AllTextContentsAsync().ConfigureAwait(false);

        _context.SessionBuilder
           .Build(
               new PropertyBagKey(key: "Result"),
               new PropertyBagValue<string>($"Found {result.Count} text contents"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public ILocator And(ILocator locator)
    {
        var argLocator = locator is InstrumentedLocator instrumented ? instrumented._locator : locator;

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(And)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"And: {argLocator}"));

        var result = _locator.And(argLocator);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public async Task BlurAsync(LocatorBlurOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(BlurAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Blurring the locator"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorBlurOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _locator.BlurAsync(options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task<LocatorBoundingBoxResult?> BoundingBoxAsync(LocatorBoundingBoxOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(BoundingBoxAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Retrieving the bounding box"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorBoundingBoxOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _locator.BoundingBoxAsync(options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task CheckAsync(LocatorCheckOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(CheckAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Checking the locator"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorCheckOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _locator.CheckAsync(options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task ClearAsync(LocatorClearOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(ClearAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Clearing the locator"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorClearOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _locator.ClearAsync(options).ConfigureAwait(false);


        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task ClickAsync(LocatorClickOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(ClickAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Clicking the locator"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorClickOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _locator.ClickAsync(options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task<int> CountAsync()
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(CountAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Counting matching elements"));

        var result = await _locator.CountAsync().ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Found {result} matching elements"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task DblClickAsync(LocatorDblClickOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(DblClickAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Double-clicking the locator"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorDblClickOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _locator.DblClickAsync(options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task DispatchEventAsync(
        string type,
        object? eventInit = null,
        LocatorDispatchEventOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(DispatchEventAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Dispatching event: {type}"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorDispatchEventOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _locator.DispatchEventAsync(type, eventInit, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task DragToAsync(ILocator target, LocatorDragToOptions? options = null)
    {
        var argTarget = target is InstrumentedLocator instrumented ? instrumented._locator : target;

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(DragToAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Dragging to target locator: {argTarget}"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorDragToOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _locator.DragToAsync(argTarget, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task<IElementHandle> ElementHandleAsync(LocatorElementHandleOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(ElementHandleAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Retrieving the element handle"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorElementHandleOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _locator.ElementHandleAsync(options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedElementHandle(_context, result);
    }

    public async Task<IReadOnlyList<IElementHandle>> ElementHandlesAsync()
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(ElementHandlesAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Retrieving all element handles"));

        var result = await _locator.ElementHandlesAsync().ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result.Select(h => new InstrumentedElementHandle(_context, h)).ToList().AsReadOnly();
    }

    public async Task<T> EvaluateAllAsync<T>(string expression, object? arg = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(EvaluateAllAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Evaluating expression on all matching elements: {expression}"));

        var result = await _locator.EvaluateAllAsync<T>(expression, arg).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<T> EvaluateAsync<T>(string expression, object? arg = null, LocatorEvaluateOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(EvaluateAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Evaluating expression: {expression}"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorEvaluateOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _locator.EvaluateAsync<T>(expression, arg, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<JsonElement?> EvaluateAsync(
        string expression,
        object? arg = null,
        LocatorEvaluateOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(EvaluateAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Evaluating expression: {expression}"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorEvaluateOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _locator.EvaluateAsync(expression, arg, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<IJSHandle> EvaluateHandleAsync(
        string expression,
        object? arg = null,
        LocatorEvaluateHandleOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(EvaluateHandleAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Evaluating handle for expression: {expression}"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorEvaluateHandleOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _locator.EvaluateHandleAsync(expression, arg, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task FillAsync(string value, LocatorFillOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(FillAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Filling the locator with value: {value}"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorFillOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _locator.FillAsync(value, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public ILocator Filter(LocatorFilterOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(Filter)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Filtering the locator"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorFilterOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.Filter(options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public async Task FocusAsync(LocatorFocusOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(FocusAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Focusing the locator"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorFocusOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _locator.FocusAsync(options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public IFrameLocator FrameLocator(string selector)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(FrameLocator)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Creating a frame locator for selector: {selector}"));

        var result = _locator.FrameLocator(selector);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedFrameLocator(_context, result);
    }

    public async Task<string?> GetAttributeAsync(string name, LocatorGetAttributeOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(GetAttributeAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Getting attribute: {name}"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorGetAttributeOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _locator.GetAttributeAsync(name, options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Attribute '{name}' value: {result}"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public ILocator GetByAltText(string text, LocatorGetByAltTextOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(GetByAltText)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Getting by alt text: {text}"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorGetByAltTextOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.GetByAltText(text, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public ILocator GetByAltText(Regex text, LocatorGetByAltTextOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(GetByAltText)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Getting by alt text (regex): {text}"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorGetByAltTextOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.GetByAltText(text, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public ILocator GetByLabel(string text, LocatorGetByLabelOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(GetByLabel)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Getting by label: {text}"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorGetByLabelOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.GetByLabel(text, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public ILocator GetByLabel(Regex text, LocatorGetByLabelOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(GetByLabel)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Getting by label (regex): {text}"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorGetByLabelOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.GetByLabel(text, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public ILocator GetByPlaceholder(string text, LocatorGetByPlaceholderOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(GetByPlaceholder)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Getting by placeholder: {text}"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorGetByPlaceholderOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.GetByPlaceholder(text, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public ILocator GetByPlaceholder(Regex text, LocatorGetByPlaceholderOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(GetByPlaceholder)))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Getting by placeholder (regex): {text}"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorGetByPlaceholderOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.GetByPlaceholder(text, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public ILocator GetByRole(AriaRole role, LocatorGetByRoleOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(GetByRole)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Getting by role: {role}"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorGetByRoleOptions)),
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
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(GetByTestId)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Getting by test ID: {testId}"));

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
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(GetByTestId)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Getting by test ID (regex): {testId}"));

        var result = _locator.GetByTestId(testId);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public ILocator GetByText(string text, LocatorGetByTextOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(GetByText)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Getting by text: {text}"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorGetByTextOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.GetByText(text, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public ILocator GetByText(Regex text, LocatorGetByTextOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(GetByText)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Getting by text (regex): {text}"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorGetByTextOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.GetByText(text, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public ILocator GetByTitle(string text, LocatorGetByTitleOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(GetByTitle)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Getting by title: {text}"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorGetByTitleOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.GetByTitle(text, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public ILocator GetByTitle(Regex text, LocatorGetByTitleOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(GetByTitle)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Getting by title (regex): {text}"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorGetByTitleOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.GetByTitle(text, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public async Task HighlightAsync()
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(HighlightAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Highlighting the locator"));

        await _locator.HighlightAsync().ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task HoverAsync(LocatorHoverOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(HoverAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Hovering over the locator"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorHoverOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _locator.HoverAsync(options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task<string> InnerHTMLAsync(LocatorInnerHTMLOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(InnerHTMLAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Getting inner HTML"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorInnerHTMLOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _locator.InnerHTMLAsync(options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Inner HTML length: {result.Length}"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<string> InnerTextAsync(LocatorInnerTextOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(InnerTextAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Getting inner text"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorInnerTextOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _locator.InnerTextAsync(options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Inner text length: {result.Length}"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<string> InputValueAsync(LocatorInputValueOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(InputValueAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Getting input value"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorInputValueOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _locator.InputValueAsync(options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Input value: {result}"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<bool> IsCheckedAsync(LocatorIsCheckedOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(IsCheckedAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Checking if element is checked"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorIsCheckedOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _locator.IsCheckedAsync(options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"IsChecked: {result}"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<bool> IsDisabledAsync(LocatorIsDisabledOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(IsDisabledAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Checking if element is disabled"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorIsDisabledOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _locator.IsDisabledAsync(options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"IsDisabled: {result}"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<bool> IsEditableAsync(LocatorIsEditableOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(IsEditableAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Checking if element is editable"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorIsEditableOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _locator.IsEditableAsync(options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "IsEditable"),
                new PropertyBagValue<string>(result.ToString()));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<bool> IsEnabledAsync(LocatorIsEnabledOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(IsEnabledAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Checking if element is enabled"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorIsEnabledOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _locator.IsEnabledAsync(options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "IsEnabled"),
                new PropertyBagValue<string>(result.ToString()));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<bool> IsHiddenAsync(LocatorIsHiddenOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(IsHiddenAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Checking if element is hidden"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorIsHiddenOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _locator.IsHiddenAsync(options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"IsHidden: {result}"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<bool> IsVisibleAsync(LocatorIsVisibleOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(IsVisibleAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Checking if element is visible"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorIsVisibleOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _locator.IsVisibleAsync(options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"IsVisible: {result}"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public ILocator Locator(string selectorOrLocator, LocatorLocatorOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(Locator)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Creating a locator for selector or locator: {selectorOrLocator}"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorLocatorOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.Locator(selectorOrLocator, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public ILocator Locator(ILocator selectorOrLocator, LocatorLocatorOptions? options = null)
    {
        var argLocator = selectorOrLocator is InstrumentedLocator instrumented ?
            instrumented._locator : selectorOrLocator;

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(Locator)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Creating a locator for selector or locator: {selectorOrLocator}"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorLocatorOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.Locator(argLocator, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public ILocator Nth(int index)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(Nth)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Selecting the {index.ToOrdinal()} element"));

        var result = _locator.Nth(index);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public ILocator Or(ILocator locator)
    {
        var argLocator = locator is InstrumentedLocator instrumented ? instrumented._locator : locator;

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(Or)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Creating a locator for selector or locator: {argLocator}"));

        var result = _locator.Or(argLocator);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public async Task PressAsync(string key, LocatorPressOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(PressAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Pressing key: {key}"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorPressOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _locator.PressAsync(key, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task PressSequentiallyAsync(string text, LocatorPressSequentiallyOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(PressSequentiallyAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Pressing keys sequentially: {text}"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorPressSequentiallyOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _locator.PressSequentiallyAsync(text, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task<byte[]> ScreenshotAsync(LocatorScreenshotOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(ScreenshotAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Taking a screenshot"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorScreenshotOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _locator.ScreenshotAsync(options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Screenshot size: {result.Length} bytes"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task ScrollIntoViewIfNeededAsync(LocatorScrollIntoViewIfNeededOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(ScrollIntoViewIfNeededAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Scrolling into view if needed"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorScrollIntoViewIfNeededOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _locator.ScrollIntoViewIfNeededAsync(options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task<IReadOnlyList<string>> SelectOptionAsync(
        string values,
        LocatorSelectOptionOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(SelectOptionAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Selecting option: {values}"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorSelectOptionOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _locator.SelectOptionAsync(values, options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Results"),
                new PropertyBagValue<string>($"Selected options: [{string.Join(", ", result)}]"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<IReadOnlyList<string>> SelectOptionAsync(
        IElementHandle values,
        LocatorSelectOptionOptions? options = null)
    {
        var argHandle = values is InstrumentedElementHandle instrumented ? instrumented.ElementHandle : values;

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(SelectOptionAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Selecting option: {argHandle}"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorSelectOptionOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _locator.SelectOptionAsync(argHandle, options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Results"),
                new PropertyBagValue<string>($"Selected options: [{string.Join(", ", result)}]"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<IReadOnlyList<string>> SelectOptionAsync(
        IEnumerable<string> values,
        LocatorSelectOptionOptions? options = null)
    {
        var enumerable = values as string[] ?? [.. values];
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(SelectOptionAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Selecting options: {string.Join(", ", enumerable)}"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorSelectOptionOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _locator.SelectOptionAsync(enumerable, options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Selected options: [{string.Join(", ", result)}]"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<IReadOnlyList<string>> SelectOptionAsync(
        SelectOptionValue values,
        LocatorSelectOptionOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(SelectOptionAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Selecting option: {JsonSerializer.Serialize(values)}"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorSelectOptionOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _locator.SelectOptionAsync(values, options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Selected options: [{string.Join(", ", result)}]"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<IReadOnlyList<string>> SelectOptionAsync(
        IEnumerable<IElementHandle> values,
        LocatorSelectOptionOptions? options = null)
    {
        var argValues = values.Select(
            v => v is InstrumentedElementHandle instrumented ?
                instrumented.ElementHandle : v);

        var elementHandles = argValues as IElementHandle[] ?? argValues.ToArray();
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(SelectOptionAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Selecting options: {string.Join(", ", argValues)}"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorSelectOptionOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _locator.SelectOptionAsync(elementHandles, options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Selected options: [{string.Join(", ", result)}]"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<IReadOnlyList<string>> SelectOptionAsync(
        IEnumerable<SelectOptionValue> values,
        LocatorSelectOptionOptions? options = null)
    {
        var selectOptionValues = values as SelectOptionValue[] ?? values.ToArray();
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(SelectOptionAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Selecting options: {string.Join(", ", selectOptionValues.ToString())}"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorSelectOptionOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _locator.SelectOptionAsync(selectOptionValues, options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Selected options: [{string.Join(", ", result)}]"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task SelectTextAsync(LocatorSelectTextOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(SelectTextAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Selecting text"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorSelectTextOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _locator.SelectTextAsync(options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task SetCheckedAsync(bool checkedState, LocatorSetCheckedOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(SetCheckedAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Setting checked state to: {checkedState}"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorSetCheckedOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _locator.SetCheckedAsync(checkedState, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task SetInputFilesAsync(string files, LocatorSetInputFilesOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(SetInputFilesAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Setting input files: {files}"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorSetInputFilesOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _locator.SetInputFilesAsync(files, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task SetInputFilesAsync(IEnumerable<string> files, LocatorSetInputFilesOptions? options = null)
    {
        var enumerable = files as string[] ?? files.ToArray();
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(SetInputFilesAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Setting input files: {string.Join(", ", enumerable)}"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorSetInputFilesOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _locator.SetInputFilesAsync(enumerable, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task SetInputFilesAsync(FilePayload files, LocatorSetInputFilesOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(SetInputFilesAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Setting input file: Name: {files.Name}; MimeType: {files.MimeType}"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorSetInputFilesOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _locator.SetInputFilesAsync(files, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task SetInputFilesAsync(IEnumerable<FilePayload> files, LocatorSetInputFilesOptions? options = null)
    {
        var filePayloads = files as FilePayload[] ?? files.ToArray();
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(SetInputFilesAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Setting input files: Count: {filePayloads.Length}"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorSetInputFilesOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _locator.SetInputFilesAsync(filePayloads, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task TapAsync(LocatorTapOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(TapAsync)}"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorTapOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _locator.TapAsync(options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task<string?> TextContentAsync(LocatorTextContentOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(TextContentAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Getting text content"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorTextContentOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _locator.TextContentAsync(options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Text content: {result}"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    [Obsolete($"Use {nameof(FillAsync)} instead.")]
    public async Task TypeAsync(string text, LocatorTypeOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(TypeAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Typing text: {text}"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorTypeOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _locator.TypeAsync(text, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task UncheckAsync(LocatorUncheckOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(UncheckAsync)))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Unchecking the checkbox"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorUncheckOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _locator.UncheckAsync(options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task WaitForAsync(LocatorWaitForOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(WaitForAsync)}"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorWaitForOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _locator.WaitForAsync(options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task<string> AriaSnapshotAsync(LocatorAriaSnapshotOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedLocator)}.{nameof(AriaSnapshotAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Taking an ARIA snapshot"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorAriaSnapshotOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _locator.AriaSnapshotAsync(options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"ARIA Snapshot: {result}"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }
}
