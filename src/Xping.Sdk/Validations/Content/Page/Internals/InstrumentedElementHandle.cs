/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Text.Json;
using Microsoft.Playwright;
using Xping.Sdk.Core.Common;
using Xping.Sdk.Core.Components;
using Xping.Sdk.Shared;

namespace Xping.Sdk.Validations.Content.Page.Internals;

internal class InstrumentedElementHandle(TestContext context, IElementHandle handle) :
    IElementHandle, IDisposable, IAsyncDisposable
{
    private readonly TestContext _context = context.RequireNotNull(nameof(context));
    private IElementHandle _handle = handle.RequireNotNull(nameof(handle));

    public IElementHandle ElementHandle => _handle;

    public IElementHandle? AsElement()
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(AsElement)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("As Element"));

        var result = _handle.AsElement();

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result != null ? new InstrumentedElementHandle(_context, result) : null;
    }

    public async Task<ElementHandleBoundingBoxResult?> BoundingBoxAsync()
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(BoundingBoxAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Get bounding box"));

        var result = await _handle.BoundingBoxAsync().ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task CheckAsync(ElementHandleCheckOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(CheckAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Check element")
            )
            .Build(
                new PropertyBagKey(key: nameof(ElementHandleCheckOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _handle.CheckAsync().ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task ClickAsync(ElementHandleClickOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(ClickAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Click element"))
            .Build(
                new PropertyBagKey(key: nameof(ElementHandleClickOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _handle.ClickAsync().ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task<IFrame?> ContentFrameAsync()
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(ContentFrameAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Get content frame"));

        var result = await _handle.ContentFrameAsync().ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result != null ? new InstrumentedFrame(_context, result) : null;
    }

    public async Task DblClickAsync(ElementHandleDblClickOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(DblClickAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Double click element"))
            .Build(
                new PropertyBagKey(key: nameof(ElementHandleDblClickOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _handle.DblClickAsync().ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task DispatchEventAsync(string type, object? eventInit = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(DispatchEventAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Dispatch event '{type}'"));

        await _handle.DispatchEventAsync(type, eventInit).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task<T> EvalOnSelectorAllAsync<T>(string selector, string expression, object? arg = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(EvalOnSelectorAllAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>(
                    $"Evaluate expression: {expression} on all elements matching selector: {selector}"));

        var result = await _handle.EvalOnSelectorAllAsync<T>(selector, expression, arg).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<T> EvalOnSelectorAsync<T>(string selector, string expression, object? arg = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(EvalOnSelectorAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>(
                    $"Evaluate expression: {expression} on first element matching selector: {selector}"));

        var result = await _handle.EvalOnSelectorAsync<T>(selector, expression, arg).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<JsonElement?> EvalOnSelectorAsync(string selector, string expression, object? arg = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(EvalOnSelectorAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>(
                    $"Evaluate expression: {expression} on first element matching selector: {selector}"));

        var result = await _handle.EvalOnSelectorAsync<JsonElement?>(selector, expression, arg).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<T> EvaluateAsync<T>(string expression, object? arg = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(EvaluateAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>(
                    $"Evaluate expression: {expression} on all elements"));

        var result = await _handle.EvaluateAsync<T>(expression, arg).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<JsonElement?> EvaluateAsync(string expression, object? arg = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(EvaluateAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>(
                    $"Evaluate expression: {expression} on all elements"));

        var result = await _handle.EvaluateAsync<JsonElement?>(expression, arg).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<IJSHandle> EvaluateHandleAsync(string expression, object? arg = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(EvaluateHandleAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>(
                    $"Evaluate expression: {expression} on all elements"));

        var result = await _handle.EvaluateHandleAsync(expression, arg).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task FillAsync(string value, ElementHandleFillOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(FillAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Fill element with value: {value}"))
            .Build(
                new PropertyBagKey(key: nameof(ElementHandleFillOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _handle.FillAsync(value, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task FocusAsync()
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(FocusAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Focus element"));

        await _handle.FocusAsync().ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task<string?> GetAttributeAsync(string name)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(GetAttributeAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get attribute: {name}"));

        var result = await _handle.GetAttributeAsync(name).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>(result ?? "null"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<Dictionary<string, IJSHandle>> GetPropertiesAsync()
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(GetPropertiesAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Get properties"));

        var result = await _handle.GetPropertiesAsync().ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"[{string.Join(",", result.Keys)}]"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<IJSHandle> GetPropertyAsync(string propertyName)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(GetPropertyAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get property: {propertyName}"));

        var result = await _handle.GetPropertyAsync(propertyName).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task HoverAsync(ElementHandleHoverOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(HoverAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Hover element"))
            .Build(
                new PropertyBagKey(key: nameof(ElementHandleHoverOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _handle.HoverAsync(options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task<string> InnerHTMLAsync()
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(InnerHTMLAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Get inner HTML"));

        var result = await _handle.InnerHTMLAsync().ConfigureAwait(false);

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

    public async Task<string> InnerTextAsync()
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(InnerTextAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Get inner text"));

        var result = await _handle.InnerTextAsync().ConfigureAwait(false);

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

    public async Task<string> InputValueAsync(ElementHandleInputValueOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(InputValueAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Get input value"))
            .Build(
                new PropertyBagKey(key: nameof(ElementHandleInputValueOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _handle.InputValueAsync(options).ConfigureAwait(false);

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

    public async Task<bool> IsCheckedAsync()
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(IsCheckedAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Is element checked"));

        var result = await _handle.IsCheckedAsync().ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Is checked: {result}"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<bool> IsDisabledAsync()
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(IsDisabledAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Is element disabled"));

        var result = await _handle.IsDisabledAsync().ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Is disabled: {result}"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<bool> IsEditableAsync()
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(IsEditableAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Is element editable"));

        var result = await _handle.IsEditableAsync().ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Is editable: {result}"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<bool> IsEnabledAsync()
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(IsEnabledAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Is element enabled"));

        var result = await _handle.IsEnabledAsync().ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Is enabled: {result}"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<bool> IsHiddenAsync()
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(IsHiddenAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Is element hidden"));

        var result = await _handle.IsHiddenAsync().ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Is hidden: {result}"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<bool> IsVisibleAsync()
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(IsVisibleAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Is element visible"));

        var result = await _handle.IsVisibleAsync().ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Is visible: {result}"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<T> JsonValueAsync<T>()
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(JsonValueAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Get JSON value"));

        var result = await _handle.JsonValueAsync<T>().ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<IFrame?> OwnerFrameAsync()
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(OwnerFrameAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Get owner frame"));

        var result = await _handle.OwnerFrameAsync().ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result != null ? new InstrumentedFrame(_context, result) : null;
    }

    public async Task PressAsync(string key, ElementHandlePressOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(PressAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Press key: {key}"))
            .Build(
                new PropertyBagKey(key: nameof(ElementHandlePressOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _handle.PressAsync(key, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task<IReadOnlyList<IElementHandle>> QuerySelectorAllAsync(string selector)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(QuerySelectorAllAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Query selector all: {selector}"));

        var result = await _handle.QuerySelectorAllAsync(selector).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Query result: {result.Count}"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result.Select(r => new InstrumentedElementHandle(_context, r)).ToList().AsReadOnly();
    }

    public async Task<IElementHandle?> QuerySelectorAsync(string selector)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(QuerySelectorAllAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Query selector all: {selector}"));

        var result = await _handle.QuerySelectorAsync(selector).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result != null ? new InstrumentedElementHandle(_context, result) : null;
    }

    public async Task<byte[]> ScreenshotAsync(ElementHandleScreenshotOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(ScreenshotAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Take screenshot"))
            .Build(
                new PropertyBagKey(key: nameof(ElementHandleScreenshotOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _handle.ScreenshotAsync(options).ConfigureAwait(false);

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

    public async Task ScrollIntoViewIfNeededAsync(ElementHandleScrollIntoViewIfNeededOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(
                    $"{nameof(InstrumentedElementHandle)}.{nameof(ScrollIntoViewIfNeededAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Scroll into view if needed"))
            .Build(
                new PropertyBagKey(key: nameof(ElementHandleScrollIntoViewIfNeededOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _handle.ScrollIntoViewIfNeededAsync(options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task<IReadOnlyList<string>> SelectOptionAsync(
        string values, ElementHandleSelectOptionOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(SelectOptionAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Select option: {values}"))
            .Build(
                new PropertyBagKey(key: nameof(ElementHandleSelectOptionOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _handle.SelectOptionAsync(values, options).ConfigureAwait(false);

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
        IElementHandle values, ElementHandleSelectOptionOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(SelectOptionAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Select option: {values}"))
            .Build(
                new PropertyBagKey(key: nameof(ElementHandleSelectOptionOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _handle.SelectOptionAsync(values, options).ConfigureAwait(false);

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
        IEnumerable<string> values, ElementHandleSelectOptionOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(SelectOptionAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Select options: {string.Join(", ", values)}"))
            .Build(
                new PropertyBagKey(key: nameof(ElementHandleSelectOptionOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _handle.SelectOptionAsync(values, options).ConfigureAwait(false);

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
        SelectOptionValue values, ElementHandleSelectOptionOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(SelectOptionAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Select option: {JsonSerializer.Serialize(values)}"))
            .Build(
                new PropertyBagKey(key: nameof(ElementHandleSelectOptionOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _handle.SelectOptionAsync(values, options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Selected option: [{string.Join(", ", result)}]"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<IReadOnlyList<string>> SelectOptionAsync(
        IEnumerable<IElementHandle> values, ElementHandleSelectOptionOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(SelectOptionAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Select options: {string.Join(", ", values)}"))
            .Build(
                new PropertyBagKey(key: nameof(ElementHandleSelectOptionOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _handle.SelectOptionAsync(values, options).ConfigureAwait(false);

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
        IEnumerable<SelectOptionValue> values, ElementHandleSelectOptionOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(SelectOptionAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Select options: {JsonSerializer.Serialize(values)}"))
            .Build(
                new PropertyBagKey(key: nameof(ElementHandleSelectOptionOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _handle.SelectOptionAsync(values, options).ConfigureAwait(false);

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

    public async Task SelectTextAsync(ElementHandleSelectTextOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(SelectTextAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Select text"))
            .Build(
                new PropertyBagKey(key: nameof(ElementHandleSelectTextOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _handle.SelectTextAsync(options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task SetCheckedAsync(bool checkedState, ElementHandleSetCheckedOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(SetCheckedAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Set checked state to: {checkedState}"))
            .Build(
                new PropertyBagKey(key: nameof(ElementHandleSetCheckedOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _handle.SetCheckedAsync(checkedState, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task SetInputFilesAsync(string files, ElementHandleSetInputFilesOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(SetInputFilesAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Set input files: {files}"))
            .Build(
                new PropertyBagKey(key: nameof(ElementHandleSetInputFilesOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _handle.SetInputFilesAsync(files, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task SetInputFilesAsync(IEnumerable<string> files, ElementHandleSetInputFilesOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(SetInputFilesAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Set input files: {string.Join(", ", files)}"))
            .Build(
                new PropertyBagKey(key: nameof(ElementHandleSetInputFilesOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _handle.SetInputFilesAsync(files, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task SetInputFilesAsync(FilePayload files, ElementHandleSetInputFilesOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(SetInputFilesAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Set input file: {files.Name}"))
            .Build(
                new PropertyBagKey(key: nameof(ElementHandleSetInputFilesOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _handle.SetInputFilesAsync(files, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task SetInputFilesAsync(
        IEnumerable<FilePayload> files,
        ElementHandleSetInputFilesOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(SetInputFilesAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>(
                    $"Set input files: " +
                    $"{ string.Join(", ", files.Select(f => $"Name: {f.Name}; MimeType: {f.MimeType}"))}"))
            .Build(
                new PropertyBagKey(key: nameof(ElementHandleSetInputFilesOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _handle.SetInputFilesAsync(files, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task TapAsync(ElementHandleTapOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(TapAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Tap element"))
            .Build(
                new PropertyBagKey(key: nameof(ElementHandleTapOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _handle.TapAsync(options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task<string?> TextContentAsync()
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(TextContentAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Get text content"));

        var result = await _handle.TextContentAsync().ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Text content length: {result?.Length ?? 0}"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    [Obsolete("Use ILocator.FillAsync instead.")]
    public async Task TypeAsync(string text, ElementHandleTypeOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(TypeAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Type text: {text}"))
            .Build(
                new PropertyBagKey(key: nameof(ElementHandleTypeOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _handle.TypeAsync(text, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task UncheckAsync(ElementHandleUncheckOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(UncheckAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Uncheck element"))
            .Build(
                new PropertyBagKey(key: nameof(ElementHandleUncheckOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _handle.UncheckAsync(options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task WaitForElementStateAsync(ElementState state, ElementHandleWaitForElementStateOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(WaitForElementStateAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Wait for element state: {state}"))
            .Build(
                new PropertyBagKey(key: nameof(ElementHandleWaitForElementStateOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _handle.WaitForElementStateAsync(state, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task<IElementHandle?> WaitForSelectorAsync(
        string selector, ElementHandleWaitForSelectorOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedElementHandle)}.{nameof(WaitForSelectorAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Wait for selector: {selector}"))
            .Build(
                new PropertyBagKey(key: nameof(ElementHandleWaitForSelectorOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _handle.WaitForSelectorAsync(selector, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    /// <summary>
    /// Releases the resources used by the browser client.
    /// </summary>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Asynchronously releases the resources used by the browser client.
    /// </summary>
    /// <returns>A task that represents the asynchronous dispose operation.</returns>
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);

        Dispose(disposing: false);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources used by the headless browser client and optionally releases the managed 
    /// resources.
    /// </summary>
    /// <param name="disposing">A flag indicating whether to release the managed resources.</param>
    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_handle is IDisposable disposable)
            {
                disposable.Dispose();
                _handle = null!;
            }
        }
    }

    /// <summary>
    /// Asynchronously performs the core logic of disposing the browser client.
    /// </summary>
    /// <returns>A task that represents the asynchronous dispose operation.</returns>
    private async ValueTask DisposeAsyncCore()
    {
        if (_handle is not null)
        {
            await _handle.DisposeAsync().ConfigureAwait(false);
        }

        _handle = null!;
    }
}
