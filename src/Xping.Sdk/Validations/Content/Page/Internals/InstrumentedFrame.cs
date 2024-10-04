using System.Data;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xping.Sdk.Core.Common;
using Xping.Sdk.Core.Components;
using Xping.Sdk.Shared;

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
               new PropertyBagValue<string>(nameof(AddScriptTagAsync)))
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
              new PropertyBagValue<string>(nameof(AddStyleTagAsync)))
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

    public Task CheckAsync(string selector, FrameCheckOptions? options = null)
    {
        _context.SessionBuilder
           .Build(
               new PropertyBagKey(key: "MethodName"),
               new PropertyBagValue<string>(nameof(CheckAsync)))
           .Build(
               new PropertyBagKey(key: nameof(selector)),
               new PropertyBagValue<string>(selector))
           .Build(
               new PropertyBagKey(key: nameof(FrameCheckOptions)),
               new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _frame.CheckAsync(selector, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task ClickAsync(string selector, FrameClickOptions? options = null)
    {
        _context.SessionBuilder
           .Build(
               new PropertyBagKey(key: "MethodName"),
               new PropertyBagValue<string>(nameof(ClickAsync)))
           .Build(
               new PropertyBagKey(key: nameof(selector)),
               new PropertyBagValue<string>(selector))
           .Build(
               new PropertyBagKey(key: nameof(FrameClickOptions)),
               new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _frame.ClickAsync(selector, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task<string> ContentAsync()
    {
        _context.SessionBuilder
           .Build(
               new PropertyBagKey(key: "MethodName"),
               new PropertyBagValue<string>(nameof(ContentAsync)));

        var result = _frame.ContentAsync();

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task DblClickAsync(string selector, FrameDblClickOptions? options = null)
    {
        _context.SessionBuilder
           .Build(
               new PropertyBagKey(key: "MethodName"),
               new PropertyBagValue<string>(nameof(DblClickAsync)))
           .Build(
               new PropertyBagKey(key: nameof(selector)),
               new PropertyBagValue<string>(selector))
           .Build(
               new PropertyBagKey(key: nameof(FrameDblClickOptions)),
               new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _frame.DblClickAsync(selector, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task DispatchEventAsync(
        string selector,
        string type,
        object? eventInit = null,
        FrameDispatchEventOptions? options = null)
    {
        _context.SessionBuilder
           .Build(
               new PropertyBagKey(key: "MethodName"),
               new PropertyBagValue<string>(nameof(DispatchEventAsync)))
           .Build(
               new PropertyBagKey(key: nameof(selector)),
               new PropertyBagValue<string>(selector))
           .Build(
               new PropertyBagKey(key: nameof(type)),
               new PropertyBagValue<string>(type))
           .Build(
               new PropertyBagKey(key: nameof(eventInit)),
               new PropertyBagValue<string>(eventInit?.ToString() ?? "Null"))
           .Build(
               new PropertyBagKey(key: nameof(FrameDispatchEventOptions)),
               new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _frame.DispatchEventAsync(selector, type, eventInit, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task DragAndDropAsync(string source, string target, FrameDragAndDropOptions? options = null)
    {
        _context.SessionBuilder
          .Build(
              new PropertyBagKey(key: "MethodName"),
              new PropertyBagValue<string>(nameof(DragAndDropAsync)))
          .Build(
              new PropertyBagKey(key: nameof(source)),
              new PropertyBagValue<string>(source))
          .Build(
              new PropertyBagKey(key: nameof(target)),
              new PropertyBagValue<string>(target))
          .Build(
              new PropertyBagKey(key: nameof(FrameDragAndDropOptions)),
              new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _frame.DragAndDropAsync(source, target, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task<T> EvalOnSelectorAllAsync<T>(string selector, string expression, object? arg = null)
    {
        _context.SessionBuilder
          .Build(
              new PropertyBagKey(key: "MethodName"),
              new PropertyBagValue<string>(nameof(EvalOnSelectorAllAsync)))
          .Build(
              new PropertyBagKey(key: nameof(selector)),
              new PropertyBagValue<string>(selector))
          .Build(
              new PropertyBagKey(key: nameof(expression)),
              new PropertyBagValue<string>(expression))
          .Build(
              new PropertyBagKey(key: nameof(arg)),
              new PropertyBagValue<string>(arg?.ToString() ?? "Null"));

        var result = _frame.EvalOnSelectorAllAsync<T>(selector, expression, arg);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task<JsonElement?> EvalOnSelectorAllAsync(string selector, string expression, object? arg = null)
    {
        _context.SessionBuilder
          .Build(
              new PropertyBagKey(key: "MethodName"),
              new PropertyBagValue<string>(nameof(EvalOnSelectorAllAsync)))
          .Build(
              new PropertyBagKey(key: nameof(selector)),
              new PropertyBagValue<string>(selector))
          .Build(
              new PropertyBagKey(key: nameof(expression)),
              new PropertyBagValue<string>(expression))
          .Build(
              new PropertyBagKey(key: nameof(arg)),
              new PropertyBagValue<string>(arg?.ToString() ?? "Null"));

        var result = _frame.EvalOnSelectorAllAsync(selector, expression, arg);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task<T> EvalOnSelectorAsync<T>(
        string selector,
        string expression,
        object? arg = null,
        FrameEvalOnSelectorOptions? options = null)
    {
        _context.SessionBuilder
          .Build(
              new PropertyBagKey(key: "MethodName"),
              new PropertyBagValue<string>(nameof(EvalOnSelectorAsync)))
          .Build(
              new PropertyBagKey(key: nameof(selector)),
              new PropertyBagValue<string>(selector))
          .Build(
              new PropertyBagKey(key: nameof(expression)),
              new PropertyBagValue<string>(expression))
          .Build(
              new PropertyBagKey(key: nameof(arg)),
              new PropertyBagValue<string>(arg?.ToString() ?? "Null"))
          .Build(
              new PropertyBagKey(key: nameof(FrameEvalOnSelectorOptions)),
              new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _frame.EvalOnSelectorAsync<T>(selector, expression, arg, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task<JsonElement?> EvalOnSelectorAsync(string selector, string expression, object? arg = null)
    {
        _context.SessionBuilder
          .Build(
              new PropertyBagKey(key: "MethodName"),
              new PropertyBagValue<string>(nameof(EvalOnSelectorAsync)))
          .Build(
              new PropertyBagKey(key: nameof(selector)),
              new PropertyBagValue<string>(selector))
          .Build(
              new PropertyBagKey(key: nameof(expression)),
              new PropertyBagValue<string>(expression))
          .Build(
              new PropertyBagKey(key: nameof(arg)),
              new PropertyBagValue<string>(arg?.ToString() ?? "Null"));

        var result = _frame.EvalOnSelectorAsync(selector, expression, arg);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task<T> EvaluateAsync<T>(string expression, object? arg = null)
    {
        _context.SessionBuilder
          .Build(
              new PropertyBagKey(key: "MethodName"),
              new PropertyBagValue<string>(nameof(EvaluateAsync)))
          .Build(
              new PropertyBagKey(key: nameof(expression)),
              new PropertyBagValue<string>(expression))
          .Build(
              new PropertyBagKey(key: nameof(arg)),
              new PropertyBagValue<string>(arg?.ToString() ?? "Null"));

        var result = _frame.EvaluateAsync<T>(expression, arg);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task<JsonElement?> EvaluateAsync(string expression, object? arg = null)
    {
        _context.SessionBuilder
           .Build(
               new PropertyBagKey(key: "MethodName"),
               new PropertyBagValue<string>(nameof(EvaluateAsync)))
           .Build(
               new PropertyBagKey(key: nameof(expression)),
               new PropertyBagValue<string>(expression))
           .Build(
               new PropertyBagKey(key: nameof(arg)),
               new PropertyBagValue<string>(arg?.ToString() ?? "Null"));

        var result = _frame.EvaluateAsync(expression, arg);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task<IJSHandle> EvaluateHandleAsync(string expression, object? arg = null)
    {
        _context.SessionBuilder
           .Build(
               new PropertyBagKey(key: "MethodName"),
               new PropertyBagValue<string>(nameof(EvaluateHandleAsync)))
           .Build(
               new PropertyBagKey(key: nameof(expression)),
               new PropertyBagValue<string>(expression))
           .Build(
               new PropertyBagKey(key: nameof(arg)),
               new PropertyBagValue<string>(arg?.ToString() ?? "Null"));

        var result = _frame.EvaluateHandleAsync(expression, arg);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task FillAsync(string selector, string value, FrameFillOptions? options = null)
    {
        _context.SessionBuilder
           .Build(
               new PropertyBagKey(key: "MethodName"),
               new PropertyBagValue<string>(nameof(FillAsync)))
            .Build(
               new PropertyBagKey(key: nameof(selector)),
               new PropertyBagValue<string>(selector))
           .Build(
               new PropertyBagKey(key: nameof(value)),
               new PropertyBagValue<string>(value))
           .Build(
               new PropertyBagKey(key: nameof(FrameFillOptions)),
               new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _frame.FillAsync(selector, value, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task FocusAsync(string selector, FrameFocusOptions? options = null)
    {
        _context.SessionBuilder
           .Build(
               new PropertyBagKey(key: "MethodName"),
               new PropertyBagValue<string>(nameof(FocusAsync)))
            .Build(
               new PropertyBagKey(key: nameof(selector)),
               new PropertyBagValue<string>(selector))
           .Build(
               new PropertyBagKey(key: nameof(FrameFocusOptions)),
               new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _frame.FocusAsync(selector, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<IElementHandle> FrameElementAsync()
    {
        _context.SessionBuilder
           .Build(
               new PropertyBagKey(key: "MethodName"),
               new PropertyBagValue<string>(nameof(FrameElementAsync)));

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
               new PropertyBagValue<string>(nameof(FrameLocator)))
            .Build(
               new PropertyBagKey(key: nameof(selector)),
               new PropertyBagValue<string>(selector));

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
               new PropertyBagValue<string>(nameof(GetAttributeAsync)))
            .Build(
               new PropertyBagKey(key: nameof(selector)),
               new PropertyBagValue<string>(selector))
            .Build(
               new PropertyBagKey(key: nameof(name)),
               new PropertyBagValue<string>(name));

        var result = await _frame.GetAttributeAsync(selector, name, options).ConfigureAwait(false);

        _context.SessionBuilder.Build(
               new PropertyBagKey(key: "Value"),
               new PropertyBagValue<string>(result ?? "Null"));

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
               new PropertyBagValue<string>(nameof(GetByAltText)))
            .Build(
               new PropertyBagKey(key: nameof(text)),
               new PropertyBagValue<string>(text))
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
               new PropertyBagValue<string>(nameof(GetByAltText)))
            .Build(
               new PropertyBagKey(key: nameof(text)),
               new PropertyBagValue<string>(text.ToString()))
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
               new PropertyBagValue<string>(nameof(GetByLabel)))
            .Build(
               new PropertyBagKey(key: nameof(text)),
               new PropertyBagValue<string>(text))
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
               new PropertyBagValue<string>(nameof(GetByLabel)))
            .Build(
               new PropertyBagKey(key: nameof(text)),
               new PropertyBagValue<string>(text.ToString()))
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
               new PropertyBagValue<string>(nameof(GetByPlaceholder)))
            .Build(
               new PropertyBagKey(key: nameof(text)),
               new PropertyBagValue<string>(text))
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
               new PropertyBagValue<string>(nameof(GetByPlaceholder)))
            .Build(
               new PropertyBagKey(key: nameof(text)),
               new PropertyBagValue<string>(text.ToString()))
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
                new PropertyBagValue<string>(nameof(GetByRole)))
            .Build(
                new PropertyBagKey(key: nameof(role)),
                new PropertyBagValue<string>(role.ToString()))
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
                new PropertyBagValue<string>(nameof(GetByTestId)))
            .Build(
                new PropertyBagKey(key: nameof(testId)),
                new PropertyBagValue<string>(testId));

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
                new PropertyBagValue<string>(nameof(GetByTestId)))
            .Build(
                new PropertyBagKey(key: nameof(testId)),
                new PropertyBagValue<string>(testId.ToString()));

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
                new PropertyBagValue<string>(nameof(GetByText)))
            .Build(
                new PropertyBagKey(key: nameof(text)),
                new PropertyBagValue<string>(text))
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
                new PropertyBagValue<string>(nameof(GetByText)))
            .Build(
                new PropertyBagKey(key: nameof(text)),
                new PropertyBagValue<string>(text.ToString()))
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
                new PropertyBagValue<string>(nameof(GetByTitle)))
            .Build(
                new PropertyBagKey(key: nameof(text)),
                new PropertyBagValue<string>(text))
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
                new PropertyBagValue<string>(nameof(GetByTitle)))
            .Build(
                new PropertyBagKey(key: nameof(text)),
                new PropertyBagValue<string>(text.ToString()))
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

    public Task<IResponse?> GotoAsync(string url, FrameGotoOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(GotoAsync)))
            .Build(
                new PropertyBagKey(key: nameof(url)),
                new PropertyBagValue<string>(url))
            .Build(
                new PropertyBagKey(key: nameof(FrameGotoOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _frame.GotoAsync(url, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task HoverAsync(string selector, FrameHoverOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(HoverAsync)))
            .Build(
                new PropertyBagKey(key: nameof(selector)),
                new PropertyBagValue<string>(selector))
            .Build(
                new PropertyBagKey(key: nameof(FrameHoverOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _frame.HoverAsync(selector, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task<string> InnerHTMLAsync(string selector, FrameInnerHTMLOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(InnerHTMLAsync)))
            .Build(
                new PropertyBagKey(key: nameof(selector)),
                new PropertyBagValue<string>(selector))
            .Build(
                new PropertyBagKey(key: nameof(FrameInnerHTMLOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _frame.InnerHTMLAsync(selector, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task<string> InnerTextAsync(string selector, FrameInnerTextOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(InnerTextAsync)))
            .Build(
                new PropertyBagKey(key: nameof(selector)),
                new PropertyBagValue<string>(selector))
            .Build(
                new PropertyBagKey(key: nameof(FrameInnerHTMLOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _frame.InnerTextAsync(selector, options);

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
                new PropertyBagValue<string>(nameof(InputValueAsync)))
            .Build(
                new PropertyBagKey(key: nameof(selector)),
                new PropertyBagValue<string>(selector))
            .Build(
                new PropertyBagKey(key: nameof(FrameInputValueOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _frame.InputValueAsync(selector, options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Value"),
                new PropertyBagValue<string>(result));

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
                new PropertyBagValue<string>(nameof(IsCheckedAsync)))
            .Build(
                new PropertyBagKey(key: nameof(selector)),
                new PropertyBagValue<string>(selector))
            .Build(
                new PropertyBagKey(key: nameof(FrameIsCheckedOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _frame.IsCheckedAsync(selector, options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Value"),
                new PropertyBagValue<string>(result.ToString()));

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
                new PropertyBagValue<string>(nameof(IsDisabledAsync)))
            .Build(
                new PropertyBagKey(key: nameof(selector)),
                new PropertyBagValue<string>(selector))
            .Build(
                new PropertyBagKey(key: nameof(FrameIsDisabledOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _frame.IsDisabledAsync(selector, options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Value"),
                new PropertyBagValue<string>(result.ToString()));

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
                new PropertyBagValue<string>(nameof(IsEditableAsync)))
            .Build(
                new PropertyBagKey(key: nameof(selector)),
                new PropertyBagValue<string>(selector))
            .Build(
                new PropertyBagKey(key: nameof(FrameIsEditableOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _frame.IsEditableAsync(selector, options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Value"),
                new PropertyBagValue<string>(result.ToString()));

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
                new PropertyBagValue<string>(nameof(IsEnabledAsync)))
            .Build(
                new PropertyBagKey(key: nameof(selector)),
                new PropertyBagValue<string>(selector))
            .Build(
                new PropertyBagKey(key: nameof(FrameIsEnabledOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _frame.IsEnabledAsync(selector, options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Value"),
                new PropertyBagValue<string>(result.ToString()));

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
                new PropertyBagValue<string>(nameof(IsHiddenAsync)))
            .Build(
                new PropertyBagKey(key: nameof(selector)),
                new PropertyBagValue<string>(selector))
            .Build(
                new PropertyBagKey(key: nameof(FrameIsHiddenOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _frame.IsHiddenAsync(selector, options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Value"),
                new PropertyBagValue<string>(result.ToString()));

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
                new PropertyBagValue<string>(nameof(IsVisibleAsync)))
            .Build(
                new PropertyBagKey(key: nameof(selector)),
                new PropertyBagValue<string>(selector))
            .Build(
                new PropertyBagKey(key: nameof(FrameIsVisibleOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _frame.IsVisibleAsync(selector, options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Value"),
                new PropertyBagValue<string>(result.ToString()));

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
                new PropertyBagValue<string>(nameof(Locator)))
            .Build(
                new PropertyBagKey(key: nameof(selector)),
                new PropertyBagValue<string>(selector))
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

    public Task PressAsync(string selector, string key, FramePressOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(PressAsync)))
            .Build(
                new PropertyBagKey(key: nameof(selector)),
                new PropertyBagValue<string>(selector))
            .Build(
                new PropertyBagKey(key: nameof(key)),
                new PropertyBagValue<string>(key))
            .Build(
                new PropertyBagKey(key: nameof(FramePressOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _frame.PressAsync(selector, key, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<IReadOnlyList<IElementHandle>> QuerySelectorAllAsync(string selector)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(QuerySelectorAllAsync)))
            .Build(
                new PropertyBagKey(key: nameof(selector)),
                new PropertyBagValue<string>(selector));

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
                new PropertyBagValue<string>(nameof(QuerySelectorAsync)))
            .Build(
                new PropertyBagKey(key: nameof(selector)),
                new PropertyBagValue<string>(selector))
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
    public Task<IResponse?> RunAndWaitForNavigationAsync(
        Func<Task> action,
        FrameRunAndWaitForNavigationOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"[Obsolete]{nameof(RunAndWaitForNavigationAsync)}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameRunAndWaitForNavigationOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _frame.RunAndWaitForNavigationAsync(action, options);

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
                new PropertyBagValue<string>(nameof(SelectOptionAsync)))
            .Build(
                new PropertyBagKey(key: nameof(selector)),
                new PropertyBagValue<string>(selector))
            .Build(
                new PropertyBagKey(key: nameof(values)),
                new PropertyBagValue<string>(values))
            .Build(
                new PropertyBagKey(key: nameof(FrameSelectOptionOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _frame.SelectOptionAsync(selector, values, options).ConfigureAwait(false);

        _context.SessionBuilder
             .Build(
                 new PropertyBagKey(key: "Options"),
                 new PropertyBagValue<string[]>([.. result]));

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
                 new PropertyBagValue<string>(nameof(SelectOptionAsync)))
             .Build(
                 new PropertyBagKey(key: nameof(selector)),
                 new PropertyBagValue<string>(selector))
             .Build(
                 new PropertyBagKey(key: nameof(values)),
                 new PropertyBagValue<string>(argHandle.ToString() ?? "Null"))
             .Build(
                 new PropertyBagKey(key: nameof(FrameSelectOptionOptions)),
                 new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _frame.SelectOptionAsync(selector, argHandle, options).ConfigureAwait(false);

        _context.SessionBuilder
             .Build(
                 new PropertyBagKey(key: "Options"),
                 new PropertyBagValue<string[]>([.. result]));

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
                 new PropertyBagValue<string>(nameof(SelectOptionAsync)))
             .Build(
                 new PropertyBagKey(key: nameof(selector)),
                 new PropertyBagValue<string>(selector))
             .Build(
                 new PropertyBagKey(key: nameof(values)),
                 new PropertyBagValue<string[]>(values.ToArray()))
             .Build(
                 new PropertyBagKey(key: nameof(FrameSelectOptionOptions)),
                 new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _frame.SelectOptionAsync(selector, values, options).ConfigureAwait(false);

        _context.SessionBuilder
             .Build(
                 new PropertyBagKey(key: "Options"),
                 new PropertyBagValue<string[]>([.. result]));

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
                 new PropertyBagValue<string>(nameof(SelectOptionAsync)))
             .Build(
                 new PropertyBagKey(key: nameof(selector)),
                 new PropertyBagValue<string>(selector))
             .Build(
                 new PropertyBagKey(key: nameof(values)),
                 new PropertyBagValue<string>(JsonSerializer.Serialize(values)))
             .Build(
                 new PropertyBagKey(key: nameof(FrameSelectOptionOptions)),
                 new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _frame.SelectOptionAsync(selector, values, options).ConfigureAwait(false);

        _context.SessionBuilder
             .Build(
                 new PropertyBagKey(key: "Options"),
                 new PropertyBagValue<string[]>([.. result]));

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
                 new PropertyBagValue<string>(nameof(SelectOptionAsync)))
             .Build(
                 new PropertyBagKey(key: nameof(selector)),
                 new PropertyBagValue<string>(selector))
             .Build(
                 new PropertyBagKey(key: nameof(values)),
                 new PropertyBagValue<string[]>(argValues.Select(v => v.ToString() ?? "Null").ToArray()))
             .Build(
                 new PropertyBagKey(key: nameof(FrameSelectOptionOptions)),
                 new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _frame.SelectOptionAsync(selector, values, options).ConfigureAwait(false);

        _context.SessionBuilder
             .Build(
                 new PropertyBagKey(key: "Options"),
                 new PropertyBagValue<string[]>([.. result]));

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
                 new PropertyBagValue<string>(nameof(SelectOptionAsync)))
             .Build(
                 new PropertyBagKey(key: nameof(selector)),
                 new PropertyBagValue<string>(selector))
             .Build(
                 new PropertyBagKey(key: nameof(values)),
                 new PropertyBagValue<string[]>(values.Select(v => JsonSerializer.Serialize(v)).ToArray()))
             .Build(
                 new PropertyBagKey(key: nameof(FrameSelectOptionOptions)),
                 new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _frame.SelectOptionAsync(selector, values, options).ConfigureAwait(false);

        _context.SessionBuilder
             .Build(
                 new PropertyBagKey(key: "Options"),
                 new PropertyBagValue<string[]>([.. result]));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task SetCheckedAsync(string selector, bool checkedState, FrameSetCheckedOptions? options = null)
    {
        _context.SessionBuilder
             .Build(
                 new PropertyBagKey(key: "MethodName"),
                 new PropertyBagValue<string>(nameof(SetCheckedAsync)))
             .Build(
                 new PropertyBagKey(key: nameof(selector)),
                 new PropertyBagValue<string>(selector))
             .Build(
                 new PropertyBagKey(key: nameof(checkedState)),
                 new PropertyBagValue<string>(checkedState.ToString()))
             .Build(
                 new PropertyBagKey(key: nameof(FrameSetCheckedOptions)),
                 new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _frame.SetCheckedAsync(selector, checkedState, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task SetContentAsync(string html, FrameSetContentOptions? options = null)
    {
        _context.SessionBuilder
             .Build(
                 new PropertyBagKey(key: "MethodName"),
                 new PropertyBagValue<string>(nameof(SetContentAsync)))
             .Build(
                 new PropertyBagKey(key: nameof(html)),
                 new PropertyBagValue<string>(html))
             .Build(
                 new PropertyBagKey(key: nameof(FrameSetContentOptions)),
                 new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _frame.SetContentAsync(html, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task SetInputFilesAsync(string selector, string files, FrameSetInputFilesOptions? options = null)
    {
        _context.SessionBuilder
             .Build(
                 new PropertyBagKey(key: "MethodName"),
                 new PropertyBagValue<string>(nameof(SetInputFilesAsync)))
             .Build(
                 new PropertyBagKey(key: nameof(selector)),
                 new PropertyBagValue<string>(selector))
             .Build(
                 new PropertyBagKey(key: nameof(files)),
                 new PropertyBagValue<string>(files))
             .Build(
                 new PropertyBagKey(key: nameof(FrameSetInputFilesOptions)),
                 new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _frame.SetInputFilesAsync(selector, files, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task SetInputFilesAsync(
        string selector,
        IEnumerable<string> files,
        FrameSetInputFilesOptions? options = null)
    {
        _context.SessionBuilder
             .Build(
                 new PropertyBagKey(key: "MethodName"),
                 new PropertyBagValue<string>(nameof(SetInputFilesAsync)))
             .Build(
                 new PropertyBagKey(key: nameof(selector)),
                 new PropertyBagValue<string>(selector))
             .Build(
                 new PropertyBagKey(key: nameof(files)),
                 new PropertyBagValue<string[]>(files.ToArray()))
             .Build(
                 new PropertyBagKey(key: nameof(FrameSetInputFilesOptions)),
                 new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _frame.SetInputFilesAsync(selector, files, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task SetInputFilesAsync(string selector, FilePayload files, FrameSetInputFilesOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(SetInputFilesAsync)))
            .Build(
                new PropertyBagKey(key: nameof(selector)),
                new PropertyBagValue<string>(selector))
            .Build(
                new PropertyBagKey(key: nameof(files)),
                new PropertyBagValue<string>($"Name: {files.Name}; MimeType: {files.MimeType}"))
            .Build(
                new PropertyBagKey(key: nameof(FrameSetInputFilesOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _frame.SetInputFilesAsync(selector, files, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task SetInputFilesAsync(
        string selector,
        IEnumerable<FilePayload> files,
        FrameSetInputFilesOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(SetInputFilesAsync)))
            .Build(
                new PropertyBagKey(key: nameof(selector)),
                new PropertyBagValue<string>(selector))
            .Build(
                new PropertyBagKey(key: nameof(files)),
                new PropertyBagValue<string[]>(
                     files.Select(f => $"Name: {f.Name}; MimeType: {f.MimeType}").ToArray()))
            .Build(
                new PropertyBagKey(key: nameof(FrameSetInputFilesOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _frame.SetInputFilesAsync(selector, files, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task TapAsync(string selector, FrameTapOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(TapAsync)))
            .Build(
                new PropertyBagKey(key: nameof(selector)),
                new PropertyBagValue<string>(selector))
            .Build(
                new PropertyBagKey(key: nameof(FrameTapOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _frame.TapAsync(selector, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<string?> TextContentAsync(string selector, FrameTextContentOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(TextContentAsync)))
            .Build(
                new PropertyBagKey(key: nameof(selector)),
                new PropertyBagValue<string>(selector))
            .Build(
                new PropertyBagKey(key: nameof(FrameTextContentOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _frame.TextContentAsync(selector, options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Value"),
                new PropertyBagValue<string>(result ?? "Null"));

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
                new PropertyBagValue<string>(nameof(TitleAsync)));

        var result = await _frame.TitleAsync().ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Value"),
                new PropertyBagValue<string>(result));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    [Obsolete("Use ILocator.FillAsync instead")]
    public Task TypeAsync(string selector, string text, FrameTypeOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(TitleAsync)))
            .Build(
                new PropertyBagKey(key: nameof(selector)),
                new PropertyBagValue<string>(selector))
            .Build(
                new PropertyBagKey(key: nameof(text)),
                new PropertyBagValue<string>(text))
            .Build(
                new PropertyBagKey(key: nameof(FrameTypeOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _frame.TypeAsync(selector, text, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task UncheckAsync(string selector, FrameUncheckOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(UncheckAsync)))
            .Build(
                new PropertyBagKey(key: nameof(selector)),
                new PropertyBagValue<string>(selector))
            .Build(
                new PropertyBagKey(key: nameof(FrameUncheckOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _frame.UncheckAsync(selector, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task<IJSHandle> WaitForFunctionAsync(
        string expression,
        object? arg = null,
        FrameWaitForFunctionOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(WaitForFunctionAsync)))
            .Build(
                new PropertyBagKey(key: nameof(expression)),
                new PropertyBagValue<string>(expression))
            .Build(
                new PropertyBagKey(key: nameof(arg)),
                new PropertyBagValue<string>(arg?.ToString() ?? "Null"))
            .Build(
                new PropertyBagKey(key: nameof(FrameUncheckOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _frame.WaitForFunctionAsync(expression, arg, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task WaitForLoadStateAsync(LoadState? state = null, FrameWaitForLoadStateOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(WaitForLoadStateAsync)))
            .Build(
                new PropertyBagKey(key: nameof(state)),
                new PropertyBagValue<string>(state?.ToString() ?? "Null"))
            .Build(
                new PropertyBagKey(key: nameof(FrameWaitForLoadStateOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _frame.WaitForLoadStateAsync(state, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    [Obsolete("Use IFrame.WaitForURLAsync instead")]
    public Task<IResponse?> WaitForNavigationAsync(FrameWaitForNavigationOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(WaitForNavigationAsync)))
            .Build(
                new PropertyBagKey(key: nameof(FrameWaitForNavigationOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _frame.WaitForNavigationAsync(options);

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
                new PropertyBagValue<string>(nameof(WaitForSelectorAsync)))
            .Build(
                new PropertyBagKey(key: nameof(selector)),
                new PropertyBagValue<string>(selector))
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

    public Task WaitForTimeoutAsync(float timeout)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(WaitForTimeoutAsync)))
            .Build(
                new PropertyBagKey(key: nameof(timeout)),
                new PropertyBagValue<string>(timeout.ToString(CultureInfo.InvariantCulture)));

        var result = _frame.WaitForTimeoutAsync(timeout);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task WaitForURLAsync(string url, FrameWaitForURLOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(WaitForURLAsync)))
            .Build(
                new PropertyBagKey(key: nameof(url)),
                new PropertyBagValue<string>(url))
            .Build(
                new PropertyBagKey(key: nameof(FrameWaitForURLOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _frame.WaitForURLAsync(url, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task WaitForURLAsync(Regex url, FrameWaitForURLOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(WaitForURLAsync)))
            .Build(
                new PropertyBagKey(key: nameof(url)),
                new PropertyBagValue<string>(url.ToString()))
            .Build(
                new PropertyBagKey(key: nameof(FrameWaitForURLOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _frame.WaitForURLAsync(url, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task WaitForURLAsync(Func<string, bool> url, FrameWaitForURLOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(WaitForURLAsync)))
            .Build(
                new PropertyBagKey(key: nameof(url)),
                new PropertyBagValue<string>(url.ToString() ?? "Null"))
            .Build(
                new PropertyBagKey(key: nameof(FrameWaitForURLOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _frame.WaitForURLAsync(url, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }
}
