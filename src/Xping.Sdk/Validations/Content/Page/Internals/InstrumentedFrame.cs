/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Data;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xping.Sdk.Core.Common;
using Xping.Sdk.Core.Components;
using Xping.Sdk.Shared;
using Xping.Sdk.Validations.Content.Page.Internals.Helpers;

namespace Xping.Sdk.Validations.Content.Page.Internals;

internal class InstrumentedFrame(TestContext context, IFrame frame) : IFrame
{
    private readonly TestContext _context = context.RequireNotNull(nameof(context));
    private readonly IFrame _frame = frame.RequireNotNull(nameof(frame));

    public IReadOnlyList<IFrame> ChildFrames =>
        _frame.ChildFrames.Select(f => new InstrumentedFrame(_context, f)).ToList().AsReadOnly();

    public bool IsDetached => _frame.IsDetached;

    public string Name => _frame.Name;

    public IPage Page => new InstrumentedPage(_context, _frame.Page);

    public IFrame? ParentFrame => _frame.ParentFrame != null ?
        new InstrumentedFrame(_context, _frame.ParentFrame) : null;

    public string Url => _frame.Url;

    public async Task<IElementHandle> AddScriptTagAsync(FrameAddScriptTagOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(AddScriptTagAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Add script tag"))
            .Build(
                new PropertyBagKey(key: nameof(FrameAddScriptTagOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _frame.AddScriptTagAsync(options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedElementHandle(_context, result);
    }

    public async Task<IElementHandle> AddStyleTagAsync(FrameAddStyleTagOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(AddStyleTagAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Add style tag"))
            .Build(
                new PropertyBagKey(key: nameof(FrameAddStyleTagOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _frame.AddStyleTagAsync(options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedElementHandle(_context, result);
    }

    public async Task CheckAsync(string selector, FrameCheckOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(CheckAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Check element matching selector: {selector}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameCheckOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _frame.CheckAsync(selector, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task ClickAsync(string selector, FrameClickOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(ClickAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Click element matching selector: {selector}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameClickOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _frame.ClickAsync(selector, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task<string> ContentAsync()
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(ContentAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Get frame content"));

        var result = await _frame.ContentAsync().ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Retrieved content with length: {result.Length}"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task DblClickAsync(string selector, FrameDblClickOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(DblClickAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Double click element matching selector: {selector}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameDblClickOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _frame.DblClickAsync(selector, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task DispatchEventAsync(
        string selector,
        string type,
        object? eventInit = null,
        FrameDispatchEventOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(DispatchEventAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Dispatch event '{type}' on element matching selector: {selector}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameDispatchEventOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _frame.DispatchEventAsync(selector, type, eventInit, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task DragAndDropAsync(string source, string target, FrameDragAndDropOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(DragAndDropAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Drag and drop element matching selector: {source} to {target}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameDragAndDropOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _frame.DragAndDropAsync(source, target, options).ConfigureAwait(false);

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
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(EvalOnSelectorAllAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>(
                    $"Evaluate expression: {expression} on all elements matching selector: {selector}"))
            .Build(
                new PropertyBagKey(key: nameof(arg)),
                new PropertyBagValue<string>(arg?.ToString() ?? "Null"));

        var result = await _frame.EvalOnSelectorAllAsync<T>(selector, expression, arg).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<JsonElement?> EvalOnSelectorAllAsync(string selector, string expression, object? arg = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(EvalOnSelectorAllAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>(
                    $"Evaluate expression: {expression} on all elements matching selector: {selector}"))
            .Build(
                new PropertyBagKey(key: nameof(arg)),
                new PropertyBagValue<string>(arg?.ToString() ?? "Null"));

        var result = await _frame.EvalOnSelectorAllAsync(selector, expression, arg).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<T> EvalOnSelectorAsync<T>(
        string selector,
        string expression,
        object? arg = null,
        FrameEvalOnSelectorOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(EvalOnSelectorAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>(
                    $"Evaluate expression: {expression} on element matching selector: {selector}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameEvalOnSelectorOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _frame.EvalOnSelectorAsync<T>(selector, expression, arg, options).ConfigureAwait(false);

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
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(EvalOnSelectorAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>(
                    $"Evaluate expression: {expression} on element matching selector: {selector}"))
            .Build(
                new PropertyBagKey(key: nameof(arg)),
                new PropertyBagValue<string>(arg?.ToString() ?? "Null"));

        var result = await _frame.EvalOnSelectorAsync(selector, expression, arg).ConfigureAwait(false);

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
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(EvaluateAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Evaluate expression: {expression}"))
            .Build(
                new PropertyBagKey(key: nameof(arg)),
                new PropertyBagValue<string>(arg?.ToString() ?? "Null"));

        var result = await _frame.EvaluateAsync<T>(expression, arg).ConfigureAwait(false);

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
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(EvaluateAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Evaluate expression: {expression}"))
            .Build(
                new PropertyBagKey(key: nameof(arg)),
                new PropertyBagValue<string>(arg?.ToString() ?? "Null"));

        var result = await _frame.EvaluateAsync(expression, arg).ConfigureAwait(false);

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
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(EvaluateHandleAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Evaluate expression: {expression}"))
            .Build(
                new PropertyBagKey(key: nameof(arg)),
                new PropertyBagValue<string>(arg?.ToString() ?? "Null"));

        var result = await _frame.EvaluateHandleAsync(expression, arg).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task FillAsync(string selector, string value, FrameFillOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(FillAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Fill element matching selector: {selector} with value: {value}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameFillOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _frame.FillAsync(selector, value, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task FocusAsync(string selector, FrameFocusOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(FocusAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Focus element matching selector: {selector}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameFocusOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _frame.FocusAsync(selector, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task<IElementHandle> FrameElementAsync()
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(FrameElementAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Get frame element"));

        var result = await _frame.FrameElementAsync().ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedElementHandle(_context, result);
    }

    public IFrameLocator FrameLocator(string selector)
    {
        _context.SessionBuilder
           .Build(
               new PropertyBagKey(key: "MethodName"),
               new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(FrameLocator)}"))
            .Build(
               new PropertyBagKey(key: "DisplayName"),
               new PropertyBagValue<string>($"Get frame locator for selector: {selector}"));

        var result = _frame.FrameLocator(selector);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedFrameLocator(_context, result);
    }

    public async Task<string?> GetAttributeAsync(string selector, string name, FrameGetAttributeOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
               new PropertyBagKey(key: "MethodName"),
               new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(GetAttributeAsync)}"))
            .Build(
               new PropertyBagKey(key: "DisplayName"),
               new PropertyBagValue<string>($"Get attribute '{name}' from element matching selector: {selector}"));

        var result = await _frame.GetAttributeAsync(selector, name, options).ConfigureAwait(false);

        _context.SessionBuilder.Build(
               new PropertyBagKey(key: "Result"),
               new PropertyBagValue<string>($"Retrieved attribute value: '{result ?? "null"}'"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public ILocator GetByAltText(string text, FrameGetByAltTextOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
               new PropertyBagKey(key: "MethodName"),
               new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(GetByAltText)}"))
            .Build(
               new PropertyBagKey(key: "DisplayName"),
               new PropertyBagValue<string>($"Get elements by alt text: {text}"))
            .Build(
               new PropertyBagKey(key: nameof(FrameGetByAltTextOptions)),
               new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _frame.GetByAltText(text, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public ILocator GetByAltText(Regex text, FrameGetByAltTextOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
               new PropertyBagKey(key: "MethodName"),
               new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(GetByAltText)}"))
            .Build(
               new PropertyBagKey(key: "DisplayName"),
               new PropertyBagValue<string>($"Get elements by alt text pattern: {text}"))
            .Build(
               new PropertyBagKey(key: nameof(FrameGetByAltTextOptions)),
               new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _frame.GetByAltText(text, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public ILocator GetByLabel(string text, FrameGetByLabelOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
               new PropertyBagKey(key: "MethodName"),
               new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(GetByLabel)}"))
            .Build(
               new PropertyBagKey(key: "DisplayName"),
               new PropertyBagValue<string>($"Get elements by label: {text}"))
            .Build(
               new PropertyBagKey(key: nameof(FrameGetByLabelOptions)),
               new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _frame.GetByLabel(text, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public ILocator GetByLabel(Regex text, FrameGetByLabelOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
               new PropertyBagKey(key: "MethodName"),
               new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(GetByLabel)}"))
            .Build(
               new PropertyBagKey(key: "DisplayName"),
               new PropertyBagValue<string>($"Get elements by label pattern: {text}"))
            .Build(
               new PropertyBagKey(key: nameof(FrameGetByLabelOptions)),
               new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _frame.GetByLabel(text, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public ILocator GetByPlaceholder(string text, FrameGetByPlaceholderOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
               new PropertyBagKey(key: "MethodName"),
               new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(GetByPlaceholder)}"))
            .Build(
               new PropertyBagKey(key: "DisplayName"),
               new PropertyBagValue<string>($"Get elements by placeholder: {text}"))
            .Build(
               new PropertyBagKey(key: nameof(FrameGetByPlaceholderOptions)),
               new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _frame.GetByPlaceholder(text, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public ILocator GetByPlaceholder(Regex text, FrameGetByPlaceholderOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
               new PropertyBagKey(key: "MethodName"),
               new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(GetByPlaceholder)}"))
            .Build(
               new PropertyBagKey(key: "DisplayName"),
               new PropertyBagValue<string>($"Get elements by placeholder pattern: {text}"))
            .Build(
               new PropertyBagKey(key: nameof(FrameGetByPlaceholderOptions)),
               new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _frame.GetByPlaceholder(text, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public ILocator GetByRole(AriaRole role, FrameGetByRoleOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(GetByRole)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get elements by role: {role}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameGetByRoleOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _frame.GetByRole(role, options);

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
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(GetByTestId)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get elements by test ID: {testId}"));

        var result = _frame.GetByTestId(testId);

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
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(GetByTestId)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get elements by test ID: {testId}"));

        var result = _frame.GetByTestId(testId);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public ILocator GetByText(string text, FrameGetByTextOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(GetByText)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get elements by text: {text}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameGetByTextOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _frame.GetByText(text, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public ILocator GetByText(Regex text, FrameGetByTextOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(GetByText)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get elements by text: {text}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameGetByTextOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _frame.GetByText(text, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public ILocator GetByTitle(string text, FrameGetByTitleOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(GetByTitle)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get elements by title: {text}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameGetByTitleOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _frame.GetByTitle(text, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public ILocator GetByTitle(Regex text, FrameGetByTitleOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(GetByTitle)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get elements by title: {text}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameGetByTitleOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _frame.GetByTitle(text, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public async Task<IResponse?> GotoAsync(string url, FrameGotoOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(GotoAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Navigate to URL: {url}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameGotoOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _frame.GotoAsync(url, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task HoverAsync(string selector, FrameHoverOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(HoverAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Hover over element matching selector: {selector}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameHoverOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _frame.HoverAsync(selector, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task<string> InnerHTMLAsync(string selector, FrameInnerHTMLOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(InnerHTMLAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get inner HTML of element matching selector: {selector}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameInnerHTMLOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _frame.InnerHTMLAsync(selector, options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Retrieved inner HTML with length: {result.Length}"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<string> InnerTextAsync(string selector, FrameInnerTextOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(InnerTextAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get inner text of element matching selector: {selector}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameInnerTextOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _frame.InnerTextAsync(selector, options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Retrieved inner text with length: {result.Length}"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<string> InputValueAsync(string selector, FrameInputValueOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(InputValueAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get input value of element matching selector: {selector}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameInputValueOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _frame.InputValueAsync(selector, options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Retrieved input value: {result}"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<bool> IsCheckedAsync(string selector, FrameIsCheckedOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(IsCheckedAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Check if element matching selector: {selector} is checked"))
            .Build(
                new PropertyBagKey(key: nameof(FrameIsCheckedOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _frame.IsCheckedAsync(selector, options).ConfigureAwait(false);

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

    public async Task<bool> IsDisabledAsync(string selector, FrameIsDisabledOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(IsDisabledAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Check if element matching selector: {selector} is disabled"))
            .Build(
                new PropertyBagKey(key: nameof(FrameIsDisabledOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _frame.IsDisabledAsync(selector, options).ConfigureAwait(false);

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

    public async Task<bool> IsEditableAsync(string selector, FrameIsEditableOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(IsEditableAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Check if element matching selector: {selector} is editable"))
            .Build(
                new PropertyBagKey(key: nameof(FrameIsEditableOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _frame.IsEditableAsync(selector, options).ConfigureAwait(false);

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

    public async Task<bool> IsEnabledAsync(string selector, FrameIsEnabledOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(IsEnabledAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Check if element matching selector: {selector} is enabled"))
            .Build(
                new PropertyBagKey(key: nameof(FrameIsEnabledOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _frame.IsEnabledAsync(selector, options).ConfigureAwait(false);

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

    public async Task<bool> IsHiddenAsync(string selector, FrameIsHiddenOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(IsHiddenAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Check if element matching selector: {selector} is hidden"))
            .Build(
                new PropertyBagKey(key: nameof(FrameIsHiddenOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _frame.IsHiddenAsync(selector, options).ConfigureAwait(false);

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

    public async Task<bool> IsVisibleAsync(string selector, FrameIsVisibleOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(IsVisibleAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Check if element matching selector: {selector} is visible"))
            .Build(
                new PropertyBagKey(key: nameof(FrameIsVisibleOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _frame.IsVisibleAsync(selector, options).ConfigureAwait(false);

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

    public ILocator Locator(string selector, FrameLocatorOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(Locator)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get locator for selector: {selector}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameLocatorOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _frame.Locator(selector, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public async Task PressAsync(string selector, string key, FramePressOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(PressAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Press key '{key}' on element matching selector: {selector}"))
            .Build(
                new PropertyBagKey(key: nameof(FramePressOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _frame.PressAsync(selector, key, options).ConfigureAwait(false);

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
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(QuerySelectorAllAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Query all elements matching selector: {selector}"));

        var result = await _frame.QuerySelectorAllAsync(selector).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result.Select(r => new InstrumentedElementHandle(_context, r)).ToList().AsReadOnly();
    }

    public async Task<IElementHandle?> QuerySelectorAsync(string selector, FrameQuerySelectorOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(QuerySelectorAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Query element matching selector: {selector}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameQuerySelectorOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _frame.QuerySelectorAsync(selector, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result != null ? new InstrumentedElementHandle(_context, result) : null;
    }

    [Obsolete("Use IFrame.WaitForUrlAsync instead")]
    public async Task<IResponse?> RunAndWaitForNavigationAsync(
        Func<Task> action,
        FrameRunAndWaitForNavigationOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(RunAndWaitForNavigationAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Run action and wait for navigation"))
            .Build(
                new PropertyBagKey(key: nameof(FrameRunAndWaitForNavigationOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _frame.RunAndWaitForNavigationAsync(action, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<IReadOnlyList<string>> SelectOptionAsync(
        string selector,
        string values,
        FrameSelectOptionOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(SelectOptionAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Select option '{values}' on element matching selector: {selector}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameSelectOptionOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _frame.SelectOptionAsync(selector, values, options).ConfigureAwait(false);

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
        string selector,
        IElementHandle values,
        FrameSelectOptionOptions? options = null)
    {
        var argHandle = values is InstrumentedElementHandle instrumented ? instrumented.ElementHandle : values;

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(SelectOptionAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Select option on element matching selector: {selector}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameSelectOptionOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _frame.SelectOptionAsync(selector, argHandle, options).ConfigureAwait(false);

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
        string selector,
        IEnumerable<string> values,
        FrameSelectOptionOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(SelectOptionAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>(
                $"Select options '{string.Join(", ", values)}' on element matching selector: {selector}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameSelectOptionOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _frame.SelectOptionAsync(selector, values, options).ConfigureAwait(false);

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
        string selector,
        SelectOptionValue values,
        FrameSelectOptionOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(SelectOptionAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>(
                $"Select option '{values.FormatSelectOptionValue()}' on element matching selector: {selector}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameSelectOptionOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _frame.SelectOptionAsync(selector, values, options).ConfigureAwait(false);

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
        string selector,
        IEnumerable<IElementHandle> values,
        FrameSelectOptionOptions? options = null)
    {
        var argValues = values.Select(
            v => v is InstrumentedElementHandle instrumented ?
                instrumented.ElementHandle : v);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(SelectOptionAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Select options on element matching selector: {selector}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameSelectOptionOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _frame.SelectOptionAsync(selector, values, options).ConfigureAwait(false);

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
        string selector,
        IEnumerable<SelectOptionValue> values,
        FrameSelectOptionOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(SelectOptionAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>(
                $"Select options '{string.Join(", ", values.Select(v => v.FormatSelectOptionValue()))}' " +
                $"on element matching selector: {selector}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameSelectOptionOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _frame.SelectOptionAsync(selector, values, options).ConfigureAwait(false);

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

    public async Task SetCheckedAsync(string selector, bool checkedState, FrameSetCheckedOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(SetCheckedAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>(
                $"Set checked state to '{checkedState}' on element matching selector: {selector}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameSetCheckedOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _frame.SetCheckedAsync(selector, checkedState, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task SetContentAsync(string html, FrameSetContentOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(SetContentAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Set frame content"))
            .Build(
                new PropertyBagKey(key: nameof(FrameSetContentOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _frame.SetContentAsync(html, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task SetInputFilesAsync(string selector, string files, FrameSetInputFilesOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(SetInputFilesAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Set input files '{files}' on element matching selector: {selector}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameSetInputFilesOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _frame.SetInputFilesAsync(selector, files, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task SetInputFilesAsync(
        string selector,
        IEnumerable<string> files,
        FrameSetInputFilesOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(SetInputFilesAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>(
                    $"Set input files '{string.Join(", ", files)}' on element matching selector: {selector}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameSetInputFilesOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _frame.SetInputFilesAsync(selector, files, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task SetInputFilesAsync(string selector, FilePayload files, FrameSetInputFilesOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(SetInputFilesAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Set input file '{files.Name}' on element matching selector: {selector}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameSetInputFilesOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _frame.SetInputFilesAsync(selector, files, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task SetInputFilesAsync(
        string selector,
        IEnumerable<FilePayload> files,
        FrameSetInputFilesOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(SetInputFilesAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>(
                    $"Set input files '{string.Join(", ", files.Select(f => f.Name))}' "+
                    $"on element matching selector: {selector}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameSetInputFilesOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _frame.SetInputFilesAsync(selector, files, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task TapAsync(string selector, FrameTapOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(TapAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Tap element matching selector: {selector}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameTapOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _frame.TapAsync(selector, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task<string?> TextContentAsync(string selector, FrameTextContentOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(TextContentAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get text content of element matching selector: {selector}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameTextContentOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _frame.TextContentAsync(selector, options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Retrieved text content with length: {result?.Length ?? 0}"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<string> TitleAsync()
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(TitleAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get the title of the frame"));

        var result = await _frame.TitleAsync().ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Retrieved title: {result}"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    [Obsolete("Use ILocator.FillAsync instead")]
    public async Task TypeAsync(string selector, string text, FrameTypeOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(TypeAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Type text '{text}' into element matching selector: {selector}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameTypeOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _frame.TypeAsync(selector, text, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task UncheckAsync(string selector, FrameUncheckOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(UncheckAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Uncheck element matching selector: {selector}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameUncheckOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _frame.UncheckAsync(selector, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task<IJSHandle> WaitForFunctionAsync(
        string expression,
        object? arg = null,
        FrameWaitForFunctionOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(WaitForFunctionAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Wait for function: {expression} to return a truthy value"))
            .Build(
                new PropertyBagKey(key: nameof(FrameWaitForFunctionOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _frame.WaitForFunctionAsync(expression, arg, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task WaitForLoadStateAsync(LoadState? state = null, FrameWaitForLoadStateOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(WaitForLoadStateAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Wait for load state: {state?.ToString() ?? "load"}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameWaitForLoadStateOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _frame.WaitForLoadStateAsync(state, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    [Obsolete("Use IFrame.WaitForURLAsync instead")]
    public async Task<IResponse?> WaitForNavigationAsync(FrameWaitForNavigationOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(WaitForNavigationAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Wait for navigation"))
            .Build(
                new PropertyBagKey(key: nameof(FrameWaitForNavigationOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _frame.WaitForNavigationAsync(options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<IElementHandle?> WaitForSelectorAsync(
        string selector,
        FrameWaitForSelectorOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(WaitForSelectorAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Wait for element matching selector: {selector}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameWaitForSelectorOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _frame.WaitForSelectorAsync(selector, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result != null ? new InstrumentedElementHandle(_context, result) : null;
    }

    public async Task WaitForTimeoutAsync(float timeout)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(WaitForTimeoutAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Wait for timeout: {timeout} ms"))
            .Build(
                new PropertyBagKey(key: nameof(timeout)),
                new PropertyBagValue<string>(timeout.ToString(CultureInfo.InvariantCulture)));

        await _frame.WaitForTimeoutAsync(timeout).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task WaitForURLAsync(string url, FrameWaitForURLOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(WaitForURLAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Wait for URL: {url}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameWaitForURLOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _frame.WaitForURLAsync(url, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task WaitForURLAsync(Regex url, FrameWaitForURLOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(WaitForURLAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Wait for URL matching regex: {url}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameWaitForURLOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _frame.WaitForURLAsync(url, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task WaitForURLAsync(Func<string, bool> url, FrameWaitForURLOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedFrame)}.{nameof(WaitForURLAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Wait for matching URL"))
            .Build(
                new PropertyBagKey(key: nameof(FrameWaitForURLOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _frame.WaitForURLAsync(url, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }
}
