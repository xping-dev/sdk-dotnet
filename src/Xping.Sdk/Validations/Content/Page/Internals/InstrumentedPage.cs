/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xping.Sdk.Core.Common;
using Xping.Sdk.Core.Components;
using Xping.Sdk.Shared;
using Xping.Sdk.Validations.Content.Page.Internals.Helpers;

namespace Xping.Sdk.Validations.Content.Page.Internals;

/// <summary>
/// Wrapper around Playwright IPage interface.
/// </summary>
internal class InstrumentedPage(TestContext context, IPage page) : IPage
{
    private readonly TestContext _context = context.RequireNotNull(nameof(context));
    private readonly IPage _page = page.RequireNotNull(nameof(page));

    internal TestContext GetTestContext() => _context;
    internal IPage GetPage() => _page;

    [Obsolete($"{nameof(IAccessibility)} class is deprecated.")]
    public IAccessibility Accessibility => _page.Accessibility;

    public IBrowserContext Context => _page.Context;

    public IReadOnlyList<IFrame> Frames => _page.Frames;

    public bool IsClosed => _page.IsClosed;

    public IKeyboard Keyboard => _page.Keyboard;

    public IFrame MainFrame => _page.MainFrame;

    public IMouse Mouse => _page.Mouse;

    public IAPIRequestContext APIRequest => _page.APIRequest;

    public ITouchscreen Touchscreen => _page.Touchscreen;

    public string Url => _page.Url;

    public IVideo? Video => _page.Video;

    public PageViewportSizeResult? ViewportSize => _page.ViewportSize;

    public IReadOnlyList<IWorker> Workers => _page.Workers;

    public IClock Clock => throw new NotImplementedException();

    public event EventHandler<IPage> Close
    {
        add { _page.Close += value; }
        remove { _page.Close -= value; }
    }

    public event EventHandler<IConsoleMessage> Console
    {
        add { _page.Console += value; }
        remove { _page.Console -= value; }
    }

    public event EventHandler<IPage> Crash
    {
        add { _page.Crash += value; }
        remove { _page.Crash -= value; }
    }

    public event EventHandler<IDialog> Dialog
    {
        add { _page.Dialog += value; }
        remove { _page.Dialog -= value; }
    }

    public event EventHandler<IPage> DOMContentLoaded
    {
        add { _page.DOMContentLoaded += value; }
        remove { _page.DOMContentLoaded -= value; }
    }

    public event EventHandler<IDownload> Download
    {
        add { _page.Download += value; }
        remove { _page.Download -= value; }
    }

    public event EventHandler<IFileChooser> FileChooser
    {
        add { _page.FileChooser += value; }
        remove { _page.FileChooser -= value; }
    }

    public event EventHandler<IFrame> FrameAttached
    {
        add { _page.FrameAttached += value; }
        remove { _page.FrameAttached -= value; }
    }

    public event EventHandler<IFrame> FrameDetached
    {
        add { _page.FrameDetached += value; }
        remove { _page.FrameDetached -= value; }
    }

    public event EventHandler<IFrame> FrameNavigated
    {
        add { _page.FrameNavigated += value; }
        remove { _page.FrameNavigated -= value; }
    }

    public event EventHandler<IPage> Load
    {
        add { _page.Load += value; }
        remove { _page.Load -= value; }
    }

    public event EventHandler<string> PageError
    {
        add { _page.PageError += value; }
        remove { _page.PageError -= value; }
    }

    public event EventHandler<IPage> Popup
    {
        add { _page.Popup += value; }
        remove { _page.Popup -= value; }
    }

    public event EventHandler<IRequest> Request
    {
        add { _page.Request += value; }
        remove { _page.Request -= value; }
    }

    public event EventHandler<IRequest> RequestFailed
    {
        add { _page.RequestFailed += value; }
        remove { _page.RequestFailed -= value; }
    }

    public event EventHandler<IRequest> RequestFinished
    {
        add { _page.RequestFinished += value; }
        remove { _page.RequestFinished -= value; }
    }

    public event EventHandler<IResponse> Response
    {
        add { _page.Response += value; }
        remove { _page.Response -= value; }
    }

    public event EventHandler<IWebSocket> WebSocket
    {
        add { _page.WebSocket += value; }
        remove { _page.WebSocket -= value; }
    }

    public event EventHandler<IWorker> Worker
    {
        add { _page.Worker += value; }
        remove { _page.Worker -= value; }
    }

    public Task AddInitScriptAsync(string? script = null, string? scriptPath = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(AddInitScriptAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Add initialization script"));

        var result = _page.AddInitScriptAsync(script, scriptPath);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<IElementHandle> AddScriptTagAsync(PageAddScriptTagOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(AddScriptTagAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Add script tag to page"))
            .Build(
                new PropertyBagKey(key: nameof(PageAddScriptTagOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options) ?? "Null"));

        var result = await _page.AddScriptTagAsync(options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedElementHandle(_context, result);
    }

    public async Task<IElementHandle> AddStyleTagAsync(PageAddStyleTagOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(AddStyleTagAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Add style tag to page"))
            .Build(
                new PropertyBagKey(key: nameof(PageAddStyleTagOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options) ?? "Null"));

        var result = await _page.AddStyleTagAsync(options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedElementHandle(_context, result);
    }

    public Task BringToFrontAsync()
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(BringToFrontAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Bring page to front"));

        var result = _page.BringToFrontAsync();

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task CheckAsync(string selector, PageCheckOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(CheckAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Check element: {selector}"))
            .Build(
                new PropertyBagKey(key: nameof(PageCheckOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options) ?? "Null"));

        var result = _page.CheckAsync(selector, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task ClickAsync(string selector, PageClickOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(ClickAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Click element: {selector}"))
            .Build(
                new PropertyBagKey(key: nameof(PageClickOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options) ?? "Null"));

        var result = _page.ClickAsync(selector, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task CloseAsync(PageCloseOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(CloseAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Close page"))
            .Build(
                new PropertyBagKey(key: nameof(PageCloseOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options) ?? "Null"));

        var result = _page.CloseAsync(options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<string> ContentAsync()
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(ContentAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Get page content"));

        var result = await _page.ContentAsync().ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Value"),
                new PropertyBagValue<string>($"Content size: {result.Length}"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task DblClickAsync(string selector, PageDblClickOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(DblClickAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Double-click element: {selector}"))
            .Build(
                new PropertyBagKey(key: nameof(PageDblClickOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options) ?? "Null"));

        var result = _page.DblClickAsync(selector, options);

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
        PageDispatchEventOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(DispatchEventAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Dispatch event '{type}' on element: {selector}"))
            .Build(
                new PropertyBagKey(key: nameof(PageDispatchEventOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.DispatchEventAsync(selector, type, eventInit, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task DragAndDropAsync(string source, string target, PageDragAndDropOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(DragAndDropAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Drag element from '{source}' to '{target}'"))
            .Build(
                new PropertyBagKey(key: nameof(PageDragAndDropOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.DragAndDropAsync(source, target, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task EmulateMediaAsync(PageEmulateMediaOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(EmulateMediaAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>("Emulate media"))
            .Build(
                new PropertyBagKey(key: nameof(PageEmulateMediaOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.EmulateMediaAsync(options);

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
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(EvalOnSelectorAllAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>(
                    $"Evaluate expression: {expression} on all elements matching selector: {selector}"))
            .Build(
                new PropertyBagKey(key: nameof(arg)),
                new PropertyBagValue<string>(arg?.ToString() ?? "Null"));

        var result = _page.EvalOnSelectorAllAsync<T>(selector, expression, arg);

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
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(EvalOnSelectorAllAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>(
                    $"Evaluate expression: {expression} on all elements matching selector: {selector}"))
            .Build(
                new PropertyBagKey(key: nameof(arg)),
                new PropertyBagValue<string>(arg?.ToString() ?? "Null"));

        var result = _page.EvalOnSelectorAllAsync(selector, expression, arg);

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
        PageEvalOnSelectorOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(EvalOnSelectorAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>(
                    $"Evaluate expression: {expression} on element matching selector: {selector}"))
            .Build(
                new PropertyBagKey(key: nameof(arg)),
                new PropertyBagValue<string>(arg?.ToString() ?? "Null"))
            .Build(
                new PropertyBagKey(key: nameof(PageEvalOnSelectorOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.EvalOnSelectorAsync<T>(selector, expression, arg, options);

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
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(EvalOnSelectorAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>(
                    $"Evaluate expression: {expression} on element matching selector: {selector}"))
            .Build(
                new PropertyBagKey(key: nameof(arg)),
                new PropertyBagValue<string>(arg?.ToString() ?? "Null"));

        var result = _page.EvalOnSelectorAsync(selector, expression, arg);

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
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(EvaluateAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Evaluate expression: {expression} on page"))
            .Build(
                new PropertyBagKey(key: nameof(arg)),
                new PropertyBagValue<string>(arg?.ToString() ?? "Null"));

        var result = _page.EvaluateAsync<T>(expression, arg);

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
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(EvaluateAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Evaluate expression: {expression} on page"))
            .Build(
                new PropertyBagKey(key: nameof(arg)),
                new PropertyBagValue<string>(arg?.ToString() ?? "Null"));

        var result = _page.EvaluateAsync(expression, arg);

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
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(EvaluateHandleAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Evaluate expression: {expression} on page"))
            .Build(
                new PropertyBagKey(key: nameof(arg)),
                new PropertyBagValue<string>(arg?.ToString() ?? "Null"));

        var result = _page.EvaluateHandleAsync(expression, arg);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task ExposeBindingAsync(string name, Action callback, PageExposeBindingOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(ExposeBindingAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Expose binding: {name} on page"))
            .Build(
                new PropertyBagKey(key: nameof(PageEvalOnSelectorOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.ExposeBindingAsync(name, callback, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task ExposeBindingAsync(string name, Action<BindingSource> callback)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(ExposeBindingAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Expose binding: {name} on page"))
            .Build(
                new PropertyBagKey(key: nameof(callback)),
                new PropertyBagValue<string>(callback.ToString() ?? "Null"));

        var result = _page.ExposeBindingAsync(name, callback);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task ExposeBindingAsync<T>(string name, Action<BindingSource, T> callback)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(ExposeBindingAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Expose binding: {name} on page"));

        var result = _page.ExposeBindingAsync(name, callback);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task ExposeBindingAsync<TResult>(string name, Func<BindingSource, TResult> callback)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(ExposeBindingAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Expose binding: {name} on page"));

        var result = _page.ExposeBindingAsync(name, callback);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task ExposeBindingAsync<TResult>(string name, Func<BindingSource, IJSHandle, TResult> callback)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(ExposeBindingAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Expose binding: {name} on page"));

        var result = _page.ExposeBindingAsync(name, callback);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task ExposeBindingAsync<T, TResult>(string name, Func<BindingSource, T, TResult> callback)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(ExposeBindingAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Expose binding: {name} on page"));

        var result = _page.ExposeBindingAsync(name, callback);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task ExposeBindingAsync<T1, T2, TResult>(string name, Func<BindingSource, T1, T2, TResult> callback)
    {
        _context.SessionBuilder
            .Build(
               new PropertyBagKey(key: "MethodName"),
               new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(ExposeBindingAsync)}"))
            .Build(
               new PropertyBagKey(key: "DisplayName"),
               new PropertyBagValue<string>($"Expose binding: {name} on page"));

        var result = _page.ExposeBindingAsync(name, callback);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task ExposeBindingAsync<T1, T2, T3, TResult>(string name, Func<BindingSource, T1, T2, T3, TResult> callback)
    {
        _context.SessionBuilder
            .Build(
               new PropertyBagKey(key: "MethodName"),
               new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(ExposeBindingAsync)}"))
            .Build(
               new PropertyBagKey(key: "DisplayName"),
               new PropertyBagValue<string>($"Expose binding: {name} on page"));

        var result = _page.ExposeBindingAsync(name, callback);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task ExposeBindingAsync<T1, T2, T3, T4, TResult>(string name, Func<BindingSource, T1, T2, T3, T4, TResult> callback)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(ExposeBindingAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Expose binding: {name} on page"));

        var result = _page.ExposeBindingAsync(name, callback);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task ExposeFunctionAsync(string name, Action callback)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(ExposeFunctionAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Expose function: {name} on page"));

        var result = _page.ExposeFunctionAsync(name, callback);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task ExposeFunctionAsync<T>(string name, Action<T> callback)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(ExposeFunctionAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Expose function: {name} on page"));

        var result = _page.ExposeFunctionAsync(name, callback);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task ExposeFunctionAsync<TResult>(string name, Func<TResult> callback)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(ExposeFunctionAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Expose function: {name} on page"));

        var result = _page.ExposeFunctionAsync(name, callback);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task ExposeFunctionAsync<T, TResult>(string name, Func<T, TResult> callback)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(ExposeFunctionAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Expose function: {name} on page"));

        var result = _page.ExposeFunctionAsync(name, callback);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task ExposeFunctionAsync<T1, T2, TResult>(string name, Func<T1, T2, TResult> callback)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(ExposeFunctionAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Expose function: {name} on page"));

        var result = _page.ExposeFunctionAsync(name, callback);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task ExposeFunctionAsync<T1, T2, T3, TResult>(string name, Func<T1, T2, T3, TResult> callback)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(ExposeFunctionAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Expose function: {name} on page"));

        var result = _page.ExposeFunctionAsync(name, callback);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task ExposeFunctionAsync<T1, T2, T3, T4, TResult>(string name, Func<T1, T2, T3, T4, TResult> callback)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(ExposeFunctionAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Expose function: {name} on page"));

        var result = _page.ExposeFunctionAsync(name, callback);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task FillAsync(string selector, string value, PageFillOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(FillAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Fill input: {selector} with value: {value} on page"))
            .Build(
                new PropertyBagKey(key: nameof(PageFillOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.FillAsync(selector, value, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task FocusAsync(string selector, PageFocusOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(FocusAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Focus element: {selector} on page"))
            .Build(
                new PropertyBagKey(key: nameof(PageFocusOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.FocusAsync(selector, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public IFrame? Frame(string name)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(Frame)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get frame: {name} on page"));

        var result = _page.Frame(name);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result != null ? new InstrumentedFrame(_context, result) : null;
    }

    public IFrame? FrameByUrl(string url)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(FrameByUrl)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get frame by URL: {url} on page"));

        var result = _page.FrameByUrl(url);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result != null ? new InstrumentedFrame(_context, result) : null;
    }

    public IFrame? FrameByUrl(Regex url)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(FrameByUrl)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get frame by URL: {url} on page"));

        var result = _page.FrameByUrl(url);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result != null ? new InstrumentedFrame(_context, result) : null;
    }

    public IFrame? FrameByUrl(Func<string, bool> url)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(FrameByUrl)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get frame by URL: {url} on page"));

        var result = _page.FrameByUrl(url);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result != null ? new InstrumentedFrame(_context, result) : null;
    }

    public IFrameLocator FrameLocator(string selector)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(FrameLocator)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get frame locator: {selector} on page"));

        var result = _page.FrameLocator(selector);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedFrameLocator(_context, result);
    }

    public async Task<string?> GetAttributeAsync(string selector, string name, PageGetAttributeOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(GetAttributeAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get attribute: {name} on element: {selector}"))
            .Build(
                new PropertyBagKey(key: nameof(PageGetAttributeOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.GetAttributeAsync(selector, name, options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>(result ?? "Null"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public ILocator GetByAltText(string text, PageGetByAltTextOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(GetByAltText)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get element by alt text: {text} on page"))
            .Build(
                new PropertyBagKey(key: nameof(PageGetByAltTextOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.GetByAltText(text, options);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Locator with {result} matching elements"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public ILocator GetByAltText(Regex text, PageGetByAltTextOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(GetByAltText)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get element by alt text: {text} on page"))
            .Build(
                new PropertyBagKey(key: nameof(PageGetByAltTextOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.GetByAltText(text, options);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Locator with {result} matching elements"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public ILocator GetByLabel(string text, PageGetByLabelOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(GetByLabel)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get element by label: {text} on page"))
            .Build(
                new PropertyBagKey(key: nameof(PageGetByLabelOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.GetByLabel(text, options);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Locator with {result} matching elements"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public ILocator GetByLabel(Regex text, PageGetByLabelOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(GetByLabel)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get element by label: {text} on page"))
            .Build(
                new PropertyBagKey(key: nameof(PageGetByLabelOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.GetByLabel(text, options);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Locator with {result} matching elements"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public ILocator GetByPlaceholder(string text, PageGetByPlaceholderOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(GetByPlaceholder)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get element by placeholder: {text} on page"))
            .Build(
                new PropertyBagKey(key: nameof(PageGetByPlaceholderOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.GetByPlaceholder(text, options);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Locator with {result} matching elements"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public ILocator GetByPlaceholder(Regex text, PageGetByPlaceholderOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(GetByPlaceholder)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get element by placeholder: {text} on page"))
            .Build(
                new PropertyBagKey(key: nameof(PageGetByPlaceholderOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.GetByPlaceholder(text, options);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Locator with {result} matching elements"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public ILocator GetByRole(AriaRole role, PageGetByRoleOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(GetByRole)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get element by role: {role} on page"))
            .Build(
                new PropertyBagKey(key: nameof(PageGetByRoleOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.GetByRole(role, options);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Locator with {result} matching elements"));

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
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(GetByTestId)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get element by testId: {testId} on page"));

        var result = _page.GetByTestId(testId);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Locator with {result} matching elements"));

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
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(GetByTestId)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get element by testId: {testId} on page"));

        var result = _page.GetByTestId(testId);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Locator with {result} matching elements"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public ILocator GetByText(string text, PageGetByTextOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(GetByText)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get element by text: {text} on page"));

        var result = _page.GetByText(text, options);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Locator with {result} matching elements"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public ILocator GetByText(Regex text, PageGetByTextOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(GetByText)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get element by text: {text} on page"));

        var result = _page.GetByText(text, options);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Locator with {result} matching elements"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public ILocator GetByTitle(string text, PageGetByTitleOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(GetByTitle)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get element by title: {text} on page"));

        var result = _page.GetByTitle(text, options);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Locator with {result} matching elements"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public ILocator GetByTitle(Regex text, PageGetByTitleOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(GetByTitle)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get element by title: {text} on page"))
            .Build(
                new PropertyBagKey(key: nameof(PageGetByTitleOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.GetByTitle(text, options);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Locator with {result} matching elements"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public Task<IResponse?> GoBackAsync(PageGoBackOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(GoBackAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Navigate back in history on page"))
            .Build(
                new PropertyBagKey(key: nameof(PageGoBackOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.GoBackAsync(options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task<IResponse?> GoForwardAsync(PageGoForwardOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(GoForwardAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Navigate forward in history on page"))
            .Build(
                new PropertyBagKey(key: nameof(PageGoForwardOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.GoForwardAsync(options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task<IResponse?> GotoAsync(string url, PageGotoOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(GotoAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Navigate to URL: {url} on page"))
            .Build(
                new PropertyBagKey(key: nameof(PageGotoOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.GotoAsync(url, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task HoverAsync(string selector, PageHoverOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(HoverAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Hover over element: {selector} on page"))
            .Build(
                new PropertyBagKey(key: nameof(PageHoverOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.HoverAsync(selector, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<string> InnerHTMLAsync(string selector, PageInnerHTMLOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(InnerHTMLAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get inner HTML of element: {selector} on page"))
            .Build(
                new PropertyBagKey(key: nameof(PageInnerHTMLOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.InnerHTMLAsync(selector, options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
            new PropertyBagKey(key: "Result"),
            new PropertyBagValue<string>(
                $"Returned HTML length: {System.Text.Encoding.UTF8.GetByteCount(result)} bytes"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<string> InnerTextAsync(string selector, PageInnerTextOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(InnerTextAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get inner text of element: {selector} on page"))
            .Build(
                new PropertyBagKey(key: nameof(PageInnerTextOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.InnerTextAsync(selector, options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>(
                    $"Returned HTML length: {System.Text.Encoding.UTF8.GetByteCount(result)} bytes"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<string> InputValueAsync(string selector, PageInputValueOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(InputValueAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get input value of element: {selector} on page"))
            .Build(
                new PropertyBagKey(key: nameof(PageInputValueOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.InputValueAsync(selector, options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>(
                    $"Input value: {result}, Length: {System.Text.Encoding.UTF8.GetByteCount(result)} bytes"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<bool> IsCheckedAsync(string selector, PageIsCheckedOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(IsCheckedAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Check if element: {selector} is checked on page"))
            .Build(
                new PropertyBagKey(key: nameof(PageIsCheckedOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.IsCheckedAsync(selector, options).ConfigureAwait(false);

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

    public async Task<bool> IsDisabledAsync(string selector, PageIsDisabledOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(IsDisabledAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Check if element: {selector} is disabled on page"))
            .Build(
                new PropertyBagKey(key: nameof(PageIsDisabledOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.IsDisabledAsync(selector, options).ConfigureAwait(false);

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

    public async Task<bool> IsEditableAsync(string selector, PageIsEditableOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(IsEditableAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Check if element: {selector} is editable on page"))
            .Build(
                new PropertyBagKey(key: nameof(PageIsEditableOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.IsEditableAsync(selector, options).ConfigureAwait(false);

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

    public async Task<bool> IsEnabledAsync(string selector, PageIsEnabledOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(IsEnabledAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Check if element: {selector} is enabled on page"))
            .Build(
                new PropertyBagKey(key: nameof(PageIsEnabledOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.IsEnabledAsync(selector, options).ConfigureAwait(false);

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

    public async Task<bool> IsHiddenAsync(string selector, PageIsHiddenOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(IsHiddenAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Check if element: {selector} is hidden on page"))
            .Build(
                new PropertyBagKey(key: nameof(PageIsHiddenOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.IsHiddenAsync(selector, options).ConfigureAwait(false);

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

    public async Task<bool> IsVisibleAsync(string selector, PageIsVisibleOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(IsVisibleAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Check if element: {selector} is visible on page"))
            .Build(
                new PropertyBagKey(key: nameof(PageIsVisibleOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.IsVisibleAsync(selector, options).ConfigureAwait(false);

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

    public ILocator Locator(string selector, PageLocatorOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(Locator)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Locate element: {selector}"))
            .Build(
                new PropertyBagKey(key: nameof(PageLocatorOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.Locator(selector, options);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Locator with {result} matching elements"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public async Task<IPage?> OpenerAsync()
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(OpenerAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get opener for page"));

        var result = await _page.OpenerAsync().ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result != null ? new InstrumentedPage(_context, result) : null;
    }

    public async Task PauseAsync()
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(PauseAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Pauses script execution"));

        await _page.PauseAsync().ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task<byte[]> PdfAsync(PagePdfOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(PdfAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Generate PDF for page"))
            .Build(
                new PropertyBagKey(key: nameof(PagePdfOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.PdfAsync(options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task PressAsync(string selector, string key, PagePressOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(PressAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Press '{key}' on element matching '{selector}'"))
            .Build(
                new PropertyBagKey(key: nameof(PagePressOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.PressAsync(selector, key, options);

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
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(QuerySelectorAllAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Query all elements matching: {selector} on page"));

        var result = await _page.QuerySelectorAllAsync(selector).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Returned {result.Count} matching elements"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result.Select(e => new InstrumentedElementHandle(_context, e)).ToList().AsReadOnly();
    }

    public async Task<IElementHandle?> QuerySelectorAsync(string selector, PageQuerySelectorOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(QuerySelectorAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Query element matching: {selector} on page"))
            .Build(
                new PropertyBagKey(key: nameof(PageQuerySelectorOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.QuerySelectorAsync(selector, options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>(
                    result != null ? "Element found." : "No matching element found."));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result != null ? new InstrumentedElementHandle(_context, result) : null;
    }

    public Task<IResponse?> ReloadAsync(PageReloadOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(ReloadAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Reload page with options: {JsonSerializer.Serialize(options)}"));

        var result = _page.ReloadAsync(options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task RouteAsync(string url, Action<IRoute> handler, PageRouteOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(RouteAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Route URL: {url} with options: {JsonSerializer.Serialize(options)}"));

        await _page.RouteAsync(url, handler, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task RouteAsync(Regex url, Action<IRoute> handler, PageRouteOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(RouteAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Route URL: {url} with options: {JsonSerializer.Serialize(options)}"));

        await _page.RouteAsync(url, handler, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();

        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task RouteAsync(Func<string, bool> url, Action<IRoute> handler, PageRouteOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(RouteAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Route URL: {url} with options: {JsonSerializer.Serialize(options)}"));

        await _page.RouteAsync(url, handler, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task RouteAsync(string url, Func<IRoute, Task> handler, PageRouteOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(RouteAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Route URL: {url} with options: {JsonSerializer.Serialize(options)}"));

        await _page.RouteAsync(url, handler, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task RouteAsync(Regex url, Func<IRoute, Task> handler, PageRouteOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(RouteAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Route URL: {url} with options: {JsonSerializer.Serialize(options)}"));

        await _page.RouteAsync(url, handler, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task RouteAsync(Func<string, bool> url, Func<IRoute, Task> handler, PageRouteOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(RouteAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Route URL: {url} with options: {JsonSerializer.Serialize(options)}"));

        await _page.RouteAsync(url, handler, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task RouteFromHARAsync(string har, PageRouteFromHAROptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(RouteFromHARAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>(
                    $"Route from HAR: {har} with options: {JsonSerializer.Serialize(options)}"));

        await _page.RouteFromHARAsync(har, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task<IConsoleMessage> RunAndWaitForConsoleMessageAsync(
        Func<Task> action,
        PageRunAndWaitForConsoleMessageOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(RunAndWaitForConsoleMessageAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Run action and wait for console message"))
            .Build(
                new PropertyBagKey(key: nameof(PageRunAndWaitForConsoleMessageOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.RunAndWaitForConsoleMessageAsync(action, options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Console message: {result.Text}"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<IDownload> RunAndWaitForDownloadAsync(
        Func<Task> action,
        PageRunAndWaitForDownloadOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(RunAndWaitForDownloadAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Run action and wait for download"))
            .Build(
                new PropertyBagKey(key: nameof(PageRunAndWaitForDownloadOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.RunAndWaitForDownloadAsync(action, options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>(
                    $"Download URL: {result.Url}, Suggested filename: {result.SuggestedFilename}"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<IFileChooser> RunAndWaitForFileChooserAsync(
        Func<Task> action,
        PageRunAndWaitForFileChooserOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(RunAndWaitForFileChooserAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Run action and wait for file chooser"))
            .Build(
                new PropertyBagKey(key: nameof(PageRunAndWaitForFileChooserOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.RunAndWaitForFileChooserAsync(action, options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>(
                    result.IsMultiple
                        ? $"File chooser with multiple selection enabled."
                        : $"File chooser with single selection enabled."));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    [Obsolete("Use IPage.WaitForURLAsync instead")]
    public async Task<IResponse?> RunAndWaitForNavigationAsync(
        Func<Task> action,
        PageRunAndWaitForNavigationOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(RunAndWaitForNavigationAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Run action and wait for navigation"))
            .Build(
                new PropertyBagKey(key: nameof(PageRunAndWaitForNavigationOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.RunAndWaitForNavigationAsync(action, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<IPage> RunAndWaitForPopupAsync(Func<Task> action, PageRunAndWaitForPopupOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(RunAndWaitForPopupAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Run action and wait for popup"))
            .Build(
                new PropertyBagKey(key: nameof(PageRunAndWaitForPopupOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.RunAndWaitForPopupAsync(action, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<IRequest> RunAndWaitForRequestAsync(
        Func<Task> action,
        string urlOrPredicate,
        PageRunAndWaitForRequestOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(RunAndWaitForRequestAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Run action and wait for request"))
            .Build(
                new PropertyBagKey(key: nameof(PageRunAndWaitForRequestOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.RunAndWaitForRequestAsync(action, urlOrPredicate, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<IRequest> RunAndWaitForRequestAsync(
        Func<Task> action,
        Regex urlOrPredicate,
        PageRunAndWaitForRequestOptions? options = null)
    {
        _context.SessionBuilder
           .Build(
               new PropertyBagKey(key: "MethodName"),
               new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(RunAndWaitForRequestAsync)}"))
           .Build(
               new PropertyBagKey(key: "DisplayName"),
               new PropertyBagValue<string>($"Run action and wait for request"))
           .Build(
               new PropertyBagKey(key: nameof(PageRunAndWaitForRequestOptions)),
               new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.RunAndWaitForRequestAsync(action, urlOrPredicate, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<IRequest> RunAndWaitForRequestAsync(
        Func<Task> action,
        Func<IRequest, bool> urlOrPredicate,
        PageRunAndWaitForRequestOptions? options = null)
    {
        _context.SessionBuilder
           .Build(
               new PropertyBagKey(key: "MethodName"),
               new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(RunAndWaitForRequestAsync)}"))
           .Build(
               new PropertyBagKey(key: "DisplayName"),
               new PropertyBagValue<string>($"Run action and wait for request"))
           .Build(
               new PropertyBagKey(key: nameof(PageRunAndWaitForRequestOptions)),
               new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.RunAndWaitForRequestAsync(action, urlOrPredicate, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<IRequest> RunAndWaitForRequestFinishedAsync(
        Func<Task> action,
        PageRunAndWaitForRequestFinishedOptions? options = null)
    {
        _context.SessionBuilder
           .Build(
               new PropertyBagKey(key: "MethodName"),
               new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(RunAndWaitForRequestFinishedAsync)}"))
           .Build(
               new PropertyBagKey(key: "DisplayName"),
               new PropertyBagValue<string>($"Run action and wait for request to finish"))
           .Build(
               new PropertyBagKey(key: nameof(PageRunAndWaitForRequestFinishedOptions)),
               new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.RunAndWaitForRequestFinishedAsync(action, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<IResponse> RunAndWaitForResponseAsync(
        Func<Task> action,
        string urlOrPredicate,
        PageRunAndWaitForResponseOptions? options = null)
    {
        _context.SessionBuilder
           .Build(
               new PropertyBagKey(key: "MethodName"),
               new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(RunAndWaitForResponseAsync)}"))
           .Build(
               new PropertyBagKey(key: "DisplayName"),
               new PropertyBagValue<string>($"Run action and wait for response"))
           .Build(
               new PropertyBagKey(key: nameof(PageRunAndWaitForResponseOptions)),
               new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.RunAndWaitForResponseAsync(action, urlOrPredicate, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<IResponse> RunAndWaitForResponseAsync(
        Func<Task> action,
        Regex urlOrPredicate,
        PageRunAndWaitForResponseOptions? options = null)
    {
        _context.SessionBuilder
           .Build(
               new PropertyBagKey(key: "MethodName"),
               new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(RunAndWaitForResponseAsync)}"))
           .Build(
               new PropertyBagKey(key: "DisplayName"),
               new PropertyBagValue<string>($"Run action and wait for response"))
           .Build(
               new PropertyBagKey(key: nameof(PageRunAndWaitForResponseOptions)),
               new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.RunAndWaitForResponseAsync(action, urlOrPredicate, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<IResponse> RunAndWaitForResponseAsync(
        Func<Task> action,
        Func<IResponse, bool> urlOrPredicate,
        PageRunAndWaitForResponseOptions? options = null)
    {
        _context.SessionBuilder
           .Build(
               new PropertyBagKey(key: "MethodName"),
               new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(RunAndWaitForResponseAsync)}"))
           .Build(
               new PropertyBagKey(key: "DisplayName"),
               new PropertyBagValue<string>($"Run action and wait for response"))
           .Build(
               new PropertyBagKey(key: nameof(PageRunAndWaitForResponseOptions)),
               new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.RunAndWaitForResponseAsync(action, urlOrPredicate, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<IWebSocket> RunAndWaitForWebSocketAsync(
        Func<Task> action,
        PageRunAndWaitForWebSocketOptions? options = null)
    {
        _context.SessionBuilder
           .Build(
               new PropertyBagKey(key: "MethodName"),
               new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(RunAndWaitForWebSocketAsync)}"))
           .Build(
               new PropertyBagKey(key: "DisplayName"),
               new PropertyBagValue<string>($"Run action and wait for WebSocket"))
           .Build(
               new PropertyBagKey(key: nameof(PageRunAndWaitForWebSocketOptions)),
               new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.RunAndWaitForWebSocketAsync(action, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task<IWorker> RunAndWaitForWorkerAsync(Func<Task> action, PageRunAndWaitForWorkerOptions? options = null)
    {
        _context.SessionBuilder
           .Build(
               new PropertyBagKey(key: "MethodName"),
               new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(RunAndWaitForWorkerAsync)}"))
           .Build(
               new PropertyBagKey(key: "DisplayName"),
               new PropertyBagValue<string>($"Run action and wait for worker"))
           .Build(
               new PropertyBagKey(key: nameof(PageRunAndWaitForWorkerOptions)),
               new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.RunAndWaitForWorkerAsync(action, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<byte[]> ScreenshotAsync(PageScreenshotOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(ScreenshotAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Take a screenshot"))
            .Build(
                new PropertyBagKey(key: nameof(PageScreenshotOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.ScreenshotAsync(options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Screenshot taken. Size: {result.Length} bytes"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<IReadOnlyList<string>> SelectOptionAsync(
        string selector,
        string values,
        PageSelectOptionOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(SelectOptionAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Select option '{values}' on element matching '{selector}'"))
            .Build(
                new PropertyBagKey(key: nameof(PageSelectOptionOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.SelectOptionAsync(selector, values, options).ConfigureAwait(false);

        _context.SessionBuilder
             .Build(
                 new PropertyBagKey(key: "Result"),
                 new PropertyBagValue<string>($"Selected options: {string.Join(", ", result)}"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<IReadOnlyList<string>> SelectOptionAsync(
        string selector,
        IElementHandle values,
        PageSelectOptionOptions? options = null)
    {
        var argHandle = values is InstrumentedElementHandle instrumented ? instrumented.ElementHandle : values;

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(SelectOptionAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Select option '{values}' on element matching '{selector}'"))
            .Build(
                new PropertyBagKey(key: nameof(PageSelectOptionOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.SelectOptionAsync(selector, values, options).ConfigureAwait(false);

        _context.SessionBuilder
             .Build(
                 new PropertyBagKey(key: "Result"),
                 new PropertyBagValue<string>($"Selected options: {string.Join(", ", result)}"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<IReadOnlyList<string>> SelectOptionAsync(
        string selector,
        IEnumerable<string> values,
        PageSelectOptionOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(SelectOptionAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>(
                    $"Select option(s) '{string.Join(',', values)}' on element matching '{selector}'"))
            .Build(
                new PropertyBagKey(key: nameof(PageSelectOptionOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.SelectOptionAsync(selector, values, options).ConfigureAwait(false);

        _context.SessionBuilder
             .Build(
                 new PropertyBagKey(key: "Result"),
                 new PropertyBagValue<string>($"Selected options: {string.Join(", ", result)}"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<IReadOnlyList<string>> SelectOptionAsync(
        string selector,
        SelectOptionValue values,
        PageSelectOptionOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(SelectOptionAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>(
                    $"Select option {values.FormatSelectOptionValue()} on element matching '{selector}'"))
            .Build(
                new PropertyBagKey(key: nameof(PageSelectOptionOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.SelectOptionAsync(selector, values, options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Selected options: {string.Join(", ", result)}"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<IReadOnlyList<string>> SelectOptionAsync(
        string selector,
        IEnumerable<IElementHandle> values,
        PageSelectOptionOptions? options = null)
    {
        var argValues = values.Select(
            v => v is InstrumentedElementHandle instrumented ?
                instrumented.ElementHandle : v);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(SelectOptionAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>(
                    $"Select options [{string.Join(", ", argValues.Select(v => v.ToString() ?? "Null"))}] on element " +
                    $"matching '{selector}'"))
            .Build(
                new PropertyBagKey(key: nameof(PageSelectOptionOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.SelectOptionAsync(selector, values, options).ConfigureAwait(false);

        _context.SessionBuilder
             .Build(
                 new PropertyBagKey(key: "Result"),
                 new PropertyBagValue<string>($"Selected options: {string.Join(", ", result)}"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<IReadOnlyList<string>> SelectOptionAsync(
        string selector,
        IEnumerable<SelectOptionValue> values,
        PageSelectOptionOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(SelectOptionAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>(
                    $"Select options [{string.Join(", ",
                        values.Select(SelectOptionValueFormatter.FormatSelectOptionValue))}] on element " +
                    $"matching '{selector}'"))
            .Build(
                new PropertyBagKey(key: nameof(PageSelectOptionOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.SelectOptionAsync(selector, values, options).ConfigureAwait(false);

        _context.SessionBuilder
             .Build(
                 new PropertyBagKey(key: "Result"),
                 new PropertyBagValue<string>($"Selected options: {string.Join(", ", result)}"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task SetCheckedAsync(string selector, bool checkedState, PageSetCheckedOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(SetCheckedAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Set checked state to '{checkedState}' on element matching '{selector}'"))
            .Build(
                new PropertyBagKey(key: nameof(PageSetCheckedOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _page.SetCheckedAsync(selector, checkedState, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task SetContentAsync(string html, PageSetContentOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(SetContentAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Set page content to HTML of length {html.Length}"))
            .Build(
                new PropertyBagKey(key: nameof(PageSetContentOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _page.SetContentAsync(html, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public void SetDefaultNavigationTimeout(float timeout)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(SetDefaultNavigationTimeout)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Set default navigation timeout to {timeout} ms")
            );

        _page.SetDefaultNavigationTimeout(timeout);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public void SetDefaultTimeout(float timeout)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(SetDefaultTimeout)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Set default timeout to {timeout} ms")
            );

        _page.SetDefaultTimeout(timeout);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task SetExtraHTTPHeadersAsync(IEnumerable<KeyValuePair<string, string>> headers)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(SetExtraHTTPHeadersAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>(
                    $"Set extra HTTP headers: {string.Join(", ", headers.Select(h => $"{h.Key}:{h.Value}"))}"));

        await _page.SetExtraHTTPHeadersAsync(headers).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task SetInputFilesAsync(string selector, string files, PageSetInputFilesOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(SetInputFilesAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Set input files '{files}' on element matching '{selector}'"))
            .Build(
                new PropertyBagKey(key: nameof(PageSetInputFilesOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _page.SetInputFilesAsync(selector, files, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task SetInputFilesAsync(
        string selector,
        IEnumerable<string> files,
        PageSetInputFilesOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(SetInputFilesAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>(
                    $"Set input files '[{string.Join(", ", files)}]' on element matching '{selector}'"))
            .Build(
                new PropertyBagKey(key: nameof(PageSetInputFilesOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _page.SetInputFilesAsync(selector, files, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task SetInputFilesAsync(string selector, FilePayload files, PageSetInputFilesOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(SetInputFilesAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Set input file '{files.Name}' on element matching '{selector}'"))
            .Build(
                new PropertyBagKey(key: nameof(PageSetInputFilesOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _page.SetInputFilesAsync(selector, files, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task SetInputFilesAsync(
        string selector,
        IEnumerable<FilePayload> files,
        PageSetInputFilesOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(SetInputFilesAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>(
                    $"Set input files '[{string.Join(", ", files.Select(f => f.Name))}]' " +
                    $"on element matching '{selector}'"))
            .Build(
                new PropertyBagKey(key: nameof(PageSetInputFilesOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _page.SetInputFilesAsync(selector, files, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task SetViewportSizeAsync(int width, int height)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(SetViewportSizeAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Set viewport size to {width}x{height}"));

        await _page.SetViewportSizeAsync(width, height).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task TapAsync(string selector, PageTapOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(TapAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Tap element matching '{selector}'"))
            .Build(
                new PropertyBagKey(key: nameof(PageTapOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _page.TapAsync(selector, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task<string?> TextContentAsync(string selector, PageTextContentOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(TextContentAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get text content of element matching '{selector}'"))
            .Build(
                new PropertyBagKey(key: nameof(PageTextContentOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.TextContentAsync(selector, options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Text content: '{result ?? "null"}'"));

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
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(TitleAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Get page title"));

        var result = await _page.TitleAsync().ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Title: '{result}'"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    [Obsolete("Use ILocator.FillAsync instead")]
    public async Task TypeAsync(string selector, string text, PageTypeOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(TypeAsync)))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Type text '{text}' into element matching '{selector}'"))
            .Build(
                new PropertyBagKey(key: nameof(PageTypeOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _page.TypeAsync(selector, text, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task UncheckAsync(string selector, PageUncheckOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(UncheckAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Uncheck element matching '{selector}'"))
            .Build(
                new PropertyBagKey(key: nameof(PageUncheckOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _page.UncheckAsync(selector, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task UnrouteAllAsync(PageUnrouteAllOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(UnrouteAllAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Unroute all routes"))
            .Build(
                new PropertyBagKey(key: nameof(PageUnrouteAllOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _page.UnrouteAllAsync(options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task UnrouteAsync(string url, Action<IRoute>? handler = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(UnrouteAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Unroute for URL '{url}'"));

        await _page.UnrouteAsync(url, handler).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task UnrouteAsync(Regex url, Action<IRoute>? handler = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(UnrouteAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Unroute for URL pattern '{url}'"));

        await _page.UnrouteAsync(url, handler).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task UnrouteAsync(Func<string, bool> url, Action<IRoute>? handler = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(UnrouteAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Unroute for URL predicate '{url}'"));

        await _page.UnrouteAsync(url, handler).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task UnrouteAsync(string url, Func<IRoute, Task> handler)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(UnrouteAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Unroute for URL '{url}'"));

        await _page.UnrouteAsync(url, handler).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task UnrouteAsync(Regex url, Func<IRoute, Task> handler)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(UnrouteAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Unroute for URL pattern '{url}'"));

        await _page.UnrouteAsync(url, handler).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task UnrouteAsync(Func<string, bool> url, Func<IRoute, Task> handler)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(UnrouteAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Unroute for URL predicate '{url}'"));

        await _page.UnrouteAsync(url, handler).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task<IConsoleMessage> WaitForConsoleMessageAsync(PageWaitForConsoleMessageOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(WaitForConsoleMessageAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Wait for console message"))
            .Build(
                new PropertyBagKey(key: nameof(PageWaitForConsoleMessageOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.WaitForConsoleMessageAsync(options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>($"Console message: '{result.Text}' of type '{result.Type}'"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<IDownload> WaitForDownloadAsync(PageWaitForDownloadOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(WaitForDownloadAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Wait for download"))
            .Build(
                new PropertyBagKey(key: nameof(PageWaitForDownloadOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.WaitForDownloadAsync(options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<IFileChooser> WaitForFileChooserAsync(PageWaitForFileChooserOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(WaitForFileChooserAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Wait for file chooser"))
            .Build(
                new PropertyBagKey(key: nameof(PageWaitForFileChooserOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.WaitForFileChooserAsync(options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<IJSHandle> WaitForFunctionAsync(
        string expression,
        object? arg = null,
        PageWaitForFunctionOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(WaitForFunctionAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Wait for function '{expression}' to return a truthy value"))
            .Build(
                new PropertyBagKey(key: nameof(PageWaitForFunctionOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.WaitForFunctionAsync(expression, arg, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task WaitForLoadStateAsync(LoadState? state = null, PageWaitForLoadStateOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(WaitForLoadStateAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Wait for load state '{state ?? LoadState.Load}'"))
            .Build(
                new PropertyBagKey(key: nameof(PageWaitForLoadStateOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _page.WaitForLoadStateAsync(state, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    [Obsolete("Use IPage.WaitForURLAsync instead")]
    public async Task<IResponse?> WaitForNavigationAsync(PageWaitForNavigationOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(WaitForNavigationAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Wait for navigation"))
            .Build(
                new PropertyBagKey(key: nameof(PageWaitForNavigationOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.WaitForNavigationAsync(options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Result"),
                new PropertyBagValue<string>(
                    $"Navigation response: '{result?.Url ?? "null"}' status {result?.Status}"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<IPage> WaitForPopupAsync(PageWaitForPopupOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(WaitForPopupAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Wait for popup"))
            .Build(
                new PropertyBagKey(key: nameof(PageWaitForPopupOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.WaitForPopupAsync(options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedPage(_context, result);
    }

    public async Task<IRequest> WaitForRequestAsync(string urlOrPredicate, PageWaitForRequestOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(WaitForRequestAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Wait for request matching '{urlOrPredicate}'"))
            .Build(
                new PropertyBagKey(key: nameof(PageWaitForRequestOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.WaitForRequestAsync(urlOrPredicate, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<IRequest> WaitForRequestAsync(Regex urlOrPredicate, PageWaitForRequestOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(WaitForRequestAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Wait for request matching '{urlOrPredicate}'"))
            .Build(
                new PropertyBagKey(key: nameof(PageWaitForRequestOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.WaitForRequestAsync(urlOrPredicate, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<IRequest> WaitForRequestAsync(
        Func<IRequest, bool> urlOrPredicate,
        PageWaitForRequestOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(WaitForRequestAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Wait for request matching '{urlOrPredicate}'"))
            .Build(
                new PropertyBagKey(key: nameof(PageWaitForRequestOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.WaitForRequestAsync(urlOrPredicate, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<IRequest> WaitForRequestFinishedAsync(PageWaitForRequestFinishedOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(WaitForRequestFinishedAsync)}"))
            .Build(
                new PropertyBagKey(key: nameof(PageWaitForRequestFinishedOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.WaitForRequestFinishedAsync(options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<IResponse> WaitForResponseAsync(string urlOrPredicate, PageWaitForResponseOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(WaitForResponseAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Wait for response matching '{urlOrPredicate}'"))
            .Build(
                new PropertyBagKey(key: nameof(PageWaitForResponseOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.WaitForResponseAsync(urlOrPredicate, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<IResponse> WaitForResponseAsync(Regex urlOrPredicate, PageWaitForResponseOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(WaitForResponseAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Wait for response matching '{urlOrPredicate}'"))
            .Build(
                new PropertyBagKey(key: nameof(PageWaitForResponseOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.WaitForResponseAsync(urlOrPredicate, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<IResponse> WaitForResponseAsync(
        Func<IResponse, bool> urlOrPredicate,
        PageWaitForResponseOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(WaitForResponseAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Wait for response matching '{urlOrPredicate}'"))
            .Build(
                new PropertyBagKey(key: nameof(PageWaitForResponseOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.WaitForResponseAsync(urlOrPredicate, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<IElementHandle?> WaitForSelectorAsync(
        string selector,
        PageWaitForSelectorOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(WaitForSelectorAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Wait for selector '{selector}'"))
            .Build(
                new PropertyBagKey(key: nameof(PageWaitForSelectorOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.WaitForSelectorAsync(selector, options).ConfigureAwait(false);

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
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(WaitForTimeoutAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Wait for timeout of {timeout} ms"));

        await _page.WaitForTimeoutAsync(timeout).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task WaitForURLAsync(string url, PageWaitForURLOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(WaitForURLAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Wait for URL '{url}'"))
            .Build(
                new PropertyBagKey(key: nameof(PageWaitForURLOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _page.WaitForURLAsync(url, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task WaitForURLAsync(Regex url, PageWaitForURLOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(WaitForURLAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Wait for URL pattern '{url}'"))
            .Build(
                new PropertyBagKey(key: nameof(PageWaitForURLOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _page.WaitForURLAsync(url, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task WaitForURLAsync(Func<string, bool> url, PageWaitForURLOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(WaitForURLAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Wait for URL matching predicate"))
            .Build(
                new PropertyBagKey(key: nameof(PageWaitForURLOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _page.WaitForURLAsync(url, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task<IWebSocket> WaitForWebSocketAsync(PageWaitForWebSocketOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(WaitForWebSocketAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Wait for WebSocket"))
            .Build(
                new PropertyBagKey(key: nameof(PageWaitForWebSocketOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.WaitForWebSocketAsync(options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<IWorker> WaitForWorkerAsync(PageWaitForWorkerOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(WaitForWorkerAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Wait for worker"))
            .Build(
                new PropertyBagKey(key: nameof(PageWaitForWorkerOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.WaitForWorkerAsync(options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task RequestGCAsync()
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(RequestGCAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Request garbage collection"));

        await _page.RequestGCAsync().ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task AddLocatorHandlerAsync(
        ILocator locator, Func<ILocator, Task> handler, PageAddLocatorHandlerOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(AddLocatorHandlerAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Add locator handler for locator '{locator}'"))
            .Build(
                new PropertyBagKey(key: nameof(PageAddLocatorHandlerOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await handler(locator).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task RemoveLocatorHandlerAsync(ILocator locator)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(RemoveLocatorHandlerAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Remove locator handler for locator '{locator}'"));

        // Assuming there is a method to remove locator handler in the _page object
        await _page.RemoveLocatorHandlerAsync(locator).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task RouteWebSocketAsync(string url, Action<IWebSocketRoute> handler)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(RouteWebSocketAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Route WebSocket for URL '{url}'"));

        await _page.RouteWebSocketAsync(url, handler).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task RouteWebSocketAsync(Regex url, Action<IWebSocketRoute> handler)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(RouteWebSocketAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Route WebSocket for URL pattern '{url}'"));

        await _page.RouteWebSocketAsync(url, handler).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task RouteWebSocketAsync(Func<string, bool> url, Action<IWebSocketRoute> handler)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(RouteWebSocketAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Route WebSocket for URL pattern '{url}'"));

        await _page.RouteWebSocketAsync(url, handler).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task AddLocatorHandlerAsync(
        ILocator locator, Func<Task> handler, PageAddLocatorHandlerOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(InstrumentedPage)}.{nameof(AddLocatorHandlerAsync)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Add locator handler for locator '{locator}'"))
            .Build(
                new PropertyBagKey(key: nameof(PageAddLocatorHandlerOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        await _page.AddLocatorHandlerAsync(locator, handler, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }
}
