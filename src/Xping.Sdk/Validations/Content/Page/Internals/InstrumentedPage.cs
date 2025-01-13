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
                new PropertyBagValue<string>(nameof(AddInitScriptAsync)))
            .Build(
                new PropertyBagKey(key: nameof(script)),
                new PropertyBagValue<string>(script ?? "Null"))
            .Build(
                new PropertyBagKey(key: nameof(scriptPath)),
                new PropertyBagValue<string>(scriptPath ?? "Null"));

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
                new PropertyBagValue<string>(nameof(AddScriptTagAsync)))
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
                new PropertyBagValue<string>(nameof(AddScriptTagAsync)))
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
                new PropertyBagValue<string>(nameof(BringToFrontAsync)));

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
                new PropertyBagValue<string>(nameof(CheckAsync)))
            .Build(
                new PropertyBagKey(key: nameof(selector)),
                new PropertyBagValue<string>(selector))
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
                new PropertyBagValue<string>(nameof(ClickAsync)))
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
                new PropertyBagValue<string>(nameof(CloseAsync)))
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
                new PropertyBagValue<string>(nameof(ContentAsync)));

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
                new PropertyBagValue<string>(nameof(DblClickAsync)))
            .Build(
                new PropertyBagKey(key: nameof(selector)),
                new PropertyBagValue<string>(selector))
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
                new PropertyBagValue<string>(nameof(DragAndDropAsync)))
            .Build(
                new PropertyBagKey(key: nameof(source)),
                new PropertyBagValue<string>(source))
            .Build(
                new PropertyBagKey(key: nameof(target)),
                new PropertyBagValue<string>(target))
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
                new PropertyBagValue<string>(nameof(EmulateMediaAsync)))
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
                new PropertyBagValue<string>(nameof(EvaluateAsync)))
            .Build(
                new PropertyBagKey(key: nameof(expression)),
                new PropertyBagValue<string>(expression))
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
                new PropertyBagValue<string>(nameof(EvaluateAsync)))
            .Build(
                new PropertyBagKey(key: nameof(expression)),
                new PropertyBagValue<string>(expression))
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
                new PropertyBagValue<string>(nameof(EvaluateHandleAsync)))
            .Build(
                new PropertyBagKey(key: nameof(expression)),
                new PropertyBagValue<string>(expression))
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
                new PropertyBagValue<string>(nameof(ExposeBindingAsync)))
            .Build(
                new PropertyBagKey(key: nameof(name)),
                new PropertyBagValue<string>(name))
            .Build(
                new PropertyBagKey(key: nameof(callback)),
                new PropertyBagValue<string>(callback.ToString() ?? "Null"))
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
                new PropertyBagValue<string>(nameof(ExposeBindingAsync)))
            .Build(
                new PropertyBagKey(key: nameof(name)),
                new PropertyBagValue<string>(name))
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
                new PropertyBagValue<string>(nameof(ExposeBindingAsync)))
            .Build(
                new PropertyBagKey(key: nameof(name)),
                new PropertyBagValue<string>(name))
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

    public Task ExposeBindingAsync<TResult>(string name, Func<BindingSource, TResult> callback)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ExposeBindingAsync)))
            .Build(
                new PropertyBagKey(key: nameof(name)),
                new PropertyBagValue<string>(name))
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

    public Task ExposeBindingAsync<TResult>(string name, Func<BindingSource, IJSHandle, TResult> callback)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ExposeBindingAsync)))
            .Build(
                new PropertyBagKey(key: nameof(name)),
                new PropertyBagValue<string>(name))
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

    public Task ExposeBindingAsync<T, TResult>(string name, Func<BindingSource, T, TResult> callback)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ExposeBindingAsync)))
            .Build(
                new PropertyBagKey(key: nameof(name)),
                new PropertyBagValue<string>(name))
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

    public Task ExposeBindingAsync<T1, T2, TResult>(string name, Func<BindingSource, T1, T2, TResult> callback)
    {
        _context.SessionBuilder
           .Build(
               new PropertyBagKey(key: "MethodName"),
               new PropertyBagValue<string>(nameof(ExposeBindingAsync)))
           .Build(
               new PropertyBagKey(key: nameof(name)),
               new PropertyBagValue<string>(name))
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

    public Task ExposeBindingAsync<T1, T2, T3, TResult>(string name, Func<BindingSource, T1, T2, T3, TResult> callback)
    {
        _context.SessionBuilder
           .Build(
               new PropertyBagKey(key: "MethodName"),
               new PropertyBagValue<string>(nameof(ExposeBindingAsync)))
           .Build(
               new PropertyBagKey(key: nameof(name)),
               new PropertyBagValue<string>(name))
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

    public Task ExposeBindingAsync<T1, T2, T3, T4, TResult>(string name, Func<BindingSource, T1, T2, T3, T4, TResult> callback)
    {
        _context.SessionBuilder
           .Build(
               new PropertyBagKey(key: "MethodName"),
               new PropertyBagValue<string>(nameof(ExposeBindingAsync)))
           .Build(
               new PropertyBagKey(key: nameof(name)),
               new PropertyBagValue<string>(name))
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

    public Task ExposeFunctionAsync(string name, Action callback)
    {
        _context.SessionBuilder
           .Build(
               new PropertyBagKey(key: "MethodName"),
               new PropertyBagValue<string>(nameof(ExposeFunctionAsync)))
           .Build(
               new PropertyBagKey(key: nameof(name)),
               new PropertyBagValue<string>(name))
           .Build(
               new PropertyBagKey(key: nameof(callback)),
               new PropertyBagValue<string>(callback.ToString() ?? "Null"));

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
               new PropertyBagValue<string>(nameof(ExposeFunctionAsync)))
           .Build(
               new PropertyBagKey(key: nameof(name)),
               new PropertyBagValue<string>(name))
           .Build(
               new PropertyBagKey(key: nameof(callback)),
               new PropertyBagValue<string>(callback.ToString() ?? "Null"));

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
               new PropertyBagValue<string>(nameof(ExposeFunctionAsync)))
           .Build(
               new PropertyBagKey(key: nameof(name)),
               new PropertyBagValue<string>(name))
           .Build(
               new PropertyBagKey(key: nameof(callback)),
               new PropertyBagValue<string>(callback.ToString() ?? "Null"));

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
               new PropertyBagValue<string>(nameof(ExposeFunctionAsync)))
           .Build(
               new PropertyBagKey(key: nameof(name)),
               new PropertyBagValue<string>(name))
           .Build(
               new PropertyBagKey(key: nameof(callback)),
               new PropertyBagValue<string>(callback.ToString() ?? "Null"));

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
               new PropertyBagValue<string>(nameof(ExposeFunctionAsync)))
           .Build(
               new PropertyBagKey(key: nameof(name)),
               new PropertyBagValue<string>(name))
           .Build(
               new PropertyBagKey(key: nameof(callback)),
               new PropertyBagValue<string>(callback.ToString() ?? "Null"));

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
               new PropertyBagValue<string>(nameof(ExposeFunctionAsync)))
           .Build(
               new PropertyBagKey(key: nameof(name)),
               new PropertyBagValue<string>(name))
           .Build(
               new PropertyBagKey(key: nameof(callback)),
               new PropertyBagValue<string>(callback.ToString() ?? "Null"));

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
               new PropertyBagValue<string>(nameof(ExposeFunctionAsync)))
           .Build(
               new PropertyBagKey(key: nameof(name)),
               new PropertyBagValue<string>(name))
           .Build(
               new PropertyBagKey(key: nameof(callback)),
               new PropertyBagValue<string>(callback.ToString() ?? "Null"));

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
               new PropertyBagValue<string>(nameof(FillAsync)))
            .Build(
               new PropertyBagKey(key: nameof(selector)),
               new PropertyBagValue<string>(selector))
           .Build(
               new PropertyBagKey(key: nameof(value)),
               new PropertyBagValue<string>(value))
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
               new PropertyBagValue<string>(nameof(FocusAsync)))
            .Build(
               new PropertyBagKey(key: nameof(selector)),
               new PropertyBagValue<string>(selector))
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
               new PropertyBagValue<string>(nameof(Frame)))
            .Build(
               new PropertyBagKey(key: nameof(name)),
               new PropertyBagValue<string>(name));

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
                new PropertyBagValue<string>(nameof(FrameByUrl)))
            .Build(
                new PropertyBagKey(key: nameof(url)),
                new PropertyBagValue<string>(url));

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
                new PropertyBagValue<string>(nameof(FrameByUrl)))
            .Build(
                new PropertyBagKey(key: nameof(url)),
                new PropertyBagValue<string>(url.ToString()));

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
                new PropertyBagValue<string>(nameof(FrameByUrl)))
            .Build(
                new PropertyBagKey(key: nameof(url)),
                new PropertyBagValue<string>(url.ToString() ?? "Null"));

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
                new PropertyBagValue<string>(nameof(FrameLocator)))
            .Build(
                new PropertyBagKey(key: nameof(selector)),
                new PropertyBagValue<string>(selector));

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
                new PropertyBagValue<string>(nameof(GetAttributeAsync)))
            .Build(
                new PropertyBagKey(key: nameof(selector)),
                new PropertyBagValue<string>(selector))
            .Build(
                new PropertyBagKey(key: nameof(name)),
                new PropertyBagValue<string>(name))
            .Build(
                new PropertyBagKey(key: nameof(PageGetAttributeOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.GetAttributeAsync(selector, name, options).ConfigureAwait(false);

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

    public ILocator GetByAltText(string text, PageGetByAltTextOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(GetByAltText)))
            .Build(
                new PropertyBagKey(key: nameof(text)),
                new PropertyBagValue<string>(text))
            .Build(
                new PropertyBagKey(key: nameof(PageGetByAltTextOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.GetByAltText(text, options);

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
                new PropertyBagValue<string>(nameof(GetByAltText)))
            .Build(
                new PropertyBagKey(key: nameof(text)),
                new PropertyBagValue<string>(text.ToString()))
            .Build(
                new PropertyBagKey(key: nameof(PageGetByAltTextOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.GetByAltText(text, options);

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
                new PropertyBagValue<string>(nameof(GetByLabel)))
            .Build(
                new PropertyBagKey(key: nameof(text)),
                new PropertyBagValue<string>(text))
            .Build(
                new PropertyBagKey(key: nameof(PageGetByLabelOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.GetByLabel(text, options);

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
                new PropertyBagValue<string>(nameof(GetByLabel)))
            .Build(
                new PropertyBagKey(key: nameof(text)),
                new PropertyBagValue<string>(text.ToString()))
            .Build(
                new PropertyBagKey(key: nameof(PageGetByLabelOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.GetByLabel(text, options);

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
                new PropertyBagValue<string>(nameof(GetByPlaceholder)))
            .Build(
                new PropertyBagKey(key: nameof(text)),
                new PropertyBagValue<string>(text))
            .Build(
                new PropertyBagKey(key: nameof(PageGetByPlaceholderOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.GetByPlaceholder(text, options);

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
                new PropertyBagValue<string>(nameof(GetByPlaceholder)))
            .Build(
                new PropertyBagKey(key: nameof(text)),
                new PropertyBagValue<string>(text.ToString()))
            .Build(
                new PropertyBagKey(key: nameof(PageGetByPlaceholderOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.GetByPlaceholder(text, options);

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
                new PropertyBagValue<string>(nameof(GetByRole)))
            .Build(
                new PropertyBagKey(key: nameof(role)),
                new PropertyBagValue<string>(role.ToString()))
            .Build(
                new PropertyBagKey(key: nameof(PageGetByRoleOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.GetByRole(role, options);

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

        var result = _page.GetByTestId(testId);

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

        var result = _page.GetByTestId(testId);

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
                new PropertyBagValue<string>(nameof(GetByText)))
            .Build(
                new PropertyBagKey(key: nameof(text)),
                new PropertyBagValue<string>(text));

        var result = _page.GetByText(text, options);

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
                new PropertyBagValue<string>(nameof(GetByText)))
            .Build(
                new PropertyBagKey(key: nameof(text)),
                new PropertyBagValue<string>(text.ToString()));

        var result = _page.GetByText(text, options);

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
                new PropertyBagValue<string>(nameof(GetByTitle)))
            .Build(
                new PropertyBagKey(key: nameof(text)),
                new PropertyBagValue<string>(text))
            .Build(
                new PropertyBagKey(key: nameof(PageGetByTitleOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.GetByTitle(text, options);

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
                new PropertyBagValue<string>(nameof(GetByTitle)))
            .Build(
                new PropertyBagKey(key: nameof(text)),
                new PropertyBagValue<string>(text.ToString()))
            .Build(
                new PropertyBagKey(key: nameof(PageGetByTitleOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.GetByTitle(text, options);

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
                new PropertyBagValue<string>(nameof(GoBackAsync)))
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
                new PropertyBagValue<string>(nameof(GoForwardAsync)))
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
                new PropertyBagValue<string>(nameof(GotoAsync)))
            .Build(
                new PropertyBagKey(key: nameof(url)),
                new PropertyBagValue<string>(url))
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
                new PropertyBagValue<string>(nameof(HoverAsync)))
            .Build(
                new PropertyBagKey(key: nameof(selector)),
                new PropertyBagValue<string>(selector))
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
                new PropertyBagValue<string>(nameof(InnerHTMLAsync)))
            .Build(
                new PropertyBagKey(key: nameof(selector)),
                new PropertyBagValue<string>(selector))
            .Build(
                new PropertyBagKey(key: nameof(PageInnerHTMLOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.InnerHTMLAsync(selector, options).ConfigureAwait(false);

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

    public async Task<string> InnerTextAsync(string selector, PageInnerTextOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(InnerTextAsync)))
            .Build(
                new PropertyBagKey(key: nameof(selector)),
                new PropertyBagValue<string>(selector))
            .Build(
                new PropertyBagKey(key: nameof(PageInnerTextOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.InnerTextAsync(selector, options).ConfigureAwait(false);

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

    public async Task<string> InputValueAsync(string selector, PageInputValueOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(InputValueAsync)))
            .Build(
                new PropertyBagKey(key: nameof(selector)),
                new PropertyBagValue<string>(selector))
            .Build(
                new PropertyBagKey(key: nameof(PageInputValueOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.InputValueAsync(selector, options).ConfigureAwait(false);

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

    public async Task<bool> IsCheckedAsync(string selector, PageIsCheckedOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(IsCheckedAsync)))
            .Build(
                new PropertyBagKey(key: nameof(selector)),
                new PropertyBagValue<string>(selector))
            .Build(
                new PropertyBagKey(key: nameof(PageIsCheckedOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.IsCheckedAsync(selector, options).ConfigureAwait(false);

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

    public async Task<bool> IsDisabledAsync(string selector, PageIsDisabledOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(IsDisabledAsync)))
            .Build(
                new PropertyBagKey(key: nameof(selector)),
                new PropertyBagValue<string>(selector))
            .Build(
                new PropertyBagKey(key: nameof(PageIsDisabledOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.IsDisabledAsync(selector, options).ConfigureAwait(false);

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

    public async Task<bool> IsEditableAsync(string selector, PageIsEditableOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(IsEditableAsync)))
            .Build(
                new PropertyBagKey(key: nameof(selector)),
                new PropertyBagValue<string>(selector))
            .Build(
                new PropertyBagKey(key: nameof(PageIsEditableOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.IsEditableAsync(selector, options).ConfigureAwait(false);

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

    public async Task<bool> IsEnabledAsync(string selector, PageIsEnabledOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(IsEnabledAsync)))
            .Build(
                new PropertyBagKey(key: nameof(selector)),
                new PropertyBagValue<string>(selector))
            .Build(
                new PropertyBagKey(key: nameof(PageIsEnabledOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.IsEnabledAsync(selector, options).ConfigureAwait(false);

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

    public async Task<bool> IsHiddenAsync(string selector, PageIsHiddenOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(IsHiddenAsync)))
            .Build(
                new PropertyBagKey(key: nameof(selector)),
                new PropertyBagValue<string>(selector))
            .Build(
                new PropertyBagKey(key: nameof(PageIsHiddenOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.IsHiddenAsync(selector, options).ConfigureAwait(false);

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

    public async Task<bool> IsVisibleAsync(string selector, PageIsVisibleOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(IsVisibleAsync)))
            .Build(
                new PropertyBagKey(key: nameof(selector)),
                new PropertyBagValue<string>(selector))
            .Build(
                new PropertyBagKey(key: nameof(PageIsVisibleOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.IsVisibleAsync(selector, options).ConfigureAwait(false);

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

    public ILocator Locator(string selector, PageLocatorOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(Locator)))
            .Build(
                new PropertyBagKey(key: nameof(selector)),
                new PropertyBagValue<string>(selector))
            .Build(
                new PropertyBagKey(key: nameof(PageLocatorOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.Locator(selector, options);

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
                new PropertyBagValue<string>(nameof(OpenerAsync)));

        var result = await _page.OpenerAsync().ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result != null ? new InstrumentedPage(_context, result) : null;
    }

    public Task PauseAsync()
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(PauseAsync)));

        var result = _page.OpenerAsync();

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task<byte[]> PdfAsync(PagePdfOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(PdfAsync)))
            .Build(
                new PropertyBagKey(key: nameof(PagePdfOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.PdfAsync(options);

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
                new PropertyBagValue<string>(nameof(PressAsync)))
            .Build(
                new PropertyBagKey(key: nameof(selector)),
                new PropertyBagValue<string>(selector))
            .Build(
                new PropertyBagKey(key: nameof(key)),
                new PropertyBagValue<string>(key))
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
                new PropertyBagValue<string>(nameof(QuerySelectorAllAsync)))
            .Build(
                new PropertyBagKey(key: nameof(selector)),
                new PropertyBagValue<string>(selector));

        var result = await _page.QuerySelectorAllAsync(selector).ConfigureAwait(false);

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
                new PropertyBagValue<string>(nameof(QuerySelectorAsync)))
            .Build(
                new PropertyBagKey(key: nameof(selector)),
                new PropertyBagValue<string>(selector))
            .Build(
                new PropertyBagKey(key: nameof(PageQuerySelectorOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.QuerySelectorAsync(selector, options).ConfigureAwait(false);

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
                new PropertyBagValue<string>(nameof(ReloadAsync)))
            .Build(
                new PropertyBagKey(key: nameof(PageReloadOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.ReloadAsync(options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task RouteAsync(string url, Action<IRoute> handler, PageRouteOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(RouteAsync)))
            .Build(
                new PropertyBagKey(key: nameof(url)),
                new PropertyBagValue<string>(url))
            .Build(
                new PropertyBagKey(key: nameof(handler)),
                new PropertyBagValue<string>(handler.ToString() ?? "Null"))
            .Build(
                new PropertyBagKey(key: nameof(PageRouteOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.RouteAsync(url, handler, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task RouteAsync(Regex url, Action<IRoute> handler, PageRouteOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(RouteAsync)))
            .Build(
                new PropertyBagKey(key: nameof(url)),
                new PropertyBagValue<string>(url.ToString()))
            .Build(
                new PropertyBagKey(key: nameof(handler)),
                new PropertyBagValue<string>(handler.ToString() ?? "Null"))
            .Build(
                new PropertyBagKey(key: nameof(PageRouteOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.RouteAsync(url, handler, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task RouteAsync(Func<string, bool> url, Action<IRoute> handler, PageRouteOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(RouteAsync)))
            .Build(
                new PropertyBagKey(key: nameof(url)),
                new PropertyBagValue<string>(url.ToString() ?? "Null"))
            .Build(
                new PropertyBagKey(key: nameof(handler)),
                new PropertyBagValue<string>(handler.ToString() ?? "Null"))
            .Build(
                new PropertyBagKey(key: nameof(PageRouteOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.RouteAsync(url, handler, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task RouteAsync(string url, Func<IRoute, Task> handler, PageRouteOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(RouteAsync)))
            .Build(
                new PropertyBagKey(key: nameof(url)),
                new PropertyBagValue<string>(url))
            .Build(
                new PropertyBagKey(key: nameof(handler)),
                new PropertyBagValue<string>(handler.ToString() ?? "Null"))
            .Build(
                new PropertyBagKey(key: nameof(PageRouteOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.RouteAsync(url, handler, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task RouteAsync(Regex url, Func<IRoute, Task> handler, PageRouteOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(RouteAsync)))
            .Build(
                new PropertyBagKey(key: nameof(url)),
                new PropertyBagValue<string>(url.ToString()))
            .Build(
                new PropertyBagKey(key: nameof(handler)),
                new PropertyBagValue<string>(handler.ToString() ?? "Null"))
            .Build(
                new PropertyBagKey(key: nameof(PageRouteOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.RouteAsync(url, handler, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task RouteAsync(Func<string, bool> url, Func<IRoute, Task> handler, PageRouteOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(RouteAsync)))
            .Build(
                new PropertyBagKey(key: nameof(url)),
                new PropertyBagValue<string>(url.ToString() ?? "Null"))
            .Build(
                new PropertyBagKey(key: nameof(handler)),
                new PropertyBagValue<string>(handler.ToString() ?? "Null"))
            .Build(
                new PropertyBagKey(key: nameof(PageRouteOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.RouteAsync(url, handler, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task RouteFromHARAsync(string har, PageRouteFromHAROptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(RouteFromHARAsync)))
            .Build(
                new PropertyBagKey(key: nameof(har)),
                new PropertyBagValue<string>(har))
            .Build(
                new PropertyBagKey(key: nameof(PageRouteFromHAROptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.RouteFromHARAsync(har, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task<IConsoleMessage> RunAndWaitForConsoleMessageAsync(
        Func<Task> action,
        PageRunAndWaitForConsoleMessageOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(RunAndWaitForConsoleMessageAsync)))
            .Build(
                new PropertyBagKey(key: nameof(action)),
                new PropertyBagValue<string>(action.ToString() ?? "Null"))
            .Build(
                new PropertyBagKey(key: nameof(PageRunAndWaitForConsoleMessageOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.RunAndWaitForConsoleMessageAsync(action, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task<IDownload> RunAndWaitForDownloadAsync(
        Func<Task> action,
        PageRunAndWaitForDownloadOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(RunAndWaitForDownloadAsync)))
            .Build(
                new PropertyBagKey(key: nameof(action)),
                new PropertyBagValue<string>(action.ToString() ?? "Null"))
            .Build(
                new PropertyBagKey(key: nameof(PageRunAndWaitForDownloadOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.RunAndWaitForDownloadAsync(action, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task<IFileChooser> RunAndWaitForFileChooserAsync(
        Func<Task> action,
        PageRunAndWaitForFileChooserOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(RunAndWaitForFileChooserAsync)))
            .Build(
                new PropertyBagKey(key: nameof(action)),
                new PropertyBagValue<string>(action.ToString() ?? "Null"))
            .Build(
                new PropertyBagKey(key: nameof(PageRunAndWaitForFileChooserOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.RunAndWaitForFileChooserAsync(action, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    [Obsolete("Use IPage.WaitForURLAsync instead")]
    public Task<IResponse?> RunAndWaitForNavigationAsync(
        Func<Task> action,
        PageRunAndWaitForNavigationOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(RunAndWaitForNavigationAsync)))
            .Build(
                new PropertyBagKey(key: nameof(action)),
                new PropertyBagValue<string>(action.ToString() ?? "Null"))
            .Build(
                new PropertyBagKey(key: nameof(PageRunAndWaitForNavigationOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.RunAndWaitForNavigationAsync(action, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task<IPage> RunAndWaitForPopupAsync(Func<Task> action, PageRunAndWaitForPopupOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(RunAndWaitForPopupAsync)))
            .Build(
                new PropertyBagKey(key: nameof(action)),
                new PropertyBagValue<string>(action.ToString() ?? "Null"))
            .Build(
                new PropertyBagKey(key: nameof(PageRunAndWaitForPopupOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.RunAndWaitForPopupAsync(action, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task<IRequest> RunAndWaitForRequestAsync(
        Func<Task> action,
        string urlOrPredicate,
        PageRunAndWaitForRequestOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(RunAndWaitForRequestAsync)))
            .Build(
                new PropertyBagKey(key: nameof(action)),
                new PropertyBagValue<string>(action.ToString() ?? "Null"))
            .Build(
                new PropertyBagKey(key: nameof(urlOrPredicate)),
                new PropertyBagValue<string>(urlOrPredicate))
            .Build(
                new PropertyBagKey(key: nameof(PageRunAndWaitForRequestOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.RunAndWaitForRequestAsync(action, urlOrPredicate, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task<IRequest> RunAndWaitForRequestAsync(
        Func<Task> action,
        Regex urlOrPredicate,
        PageRunAndWaitForRequestOptions? options = null)
    {
        _context.SessionBuilder
           .Build(
               new PropertyBagKey(key: "MethodName"),
               new PropertyBagValue<string>(nameof(RunAndWaitForRequestAsync)))
           .Build(
               new PropertyBagKey(key: nameof(action)),
               new PropertyBagValue<string>(action.ToString() ?? "Null"))
           .Build(
               new PropertyBagKey(key: nameof(urlOrPredicate)),
               new PropertyBagValue<string>(urlOrPredicate.ToString()))
           .Build(
               new PropertyBagKey(key: nameof(PageRunAndWaitForRequestOptions)),
               new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.RunAndWaitForRequestAsync(action, urlOrPredicate, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task<IRequest> RunAndWaitForRequestAsync(
        Func<Task> action,
        Func<IRequest, bool> urlOrPredicate,
        PageRunAndWaitForRequestOptions? options = null)
    {
        _context.SessionBuilder
           .Build(
               new PropertyBagKey(key: "MethodName"),
               new PropertyBagValue<string>(nameof(RunAndWaitForRequestAsync)))
           .Build(
               new PropertyBagKey(key: nameof(action)),
               new PropertyBagValue<string>(action.ToString() ?? "Null"))
           .Build(
               new PropertyBagKey(key: nameof(urlOrPredicate)),
               new PropertyBagValue<string>(urlOrPredicate.ToString() ?? "Null"))
           .Build(
               new PropertyBagKey(key: nameof(PageRunAndWaitForRequestOptions)),
               new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.RunAndWaitForRequestAsync(action, urlOrPredicate, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task<IRequest> RunAndWaitForRequestFinishedAsync(
        Func<Task> action,
        PageRunAndWaitForRequestFinishedOptions? options = null)
    {
        _context.SessionBuilder
           .Build(
               new PropertyBagKey(key: "MethodName"),
               new PropertyBagValue<string>(nameof(RunAndWaitForRequestFinishedAsync)))
           .Build(
               new PropertyBagKey(key: nameof(action)),
               new PropertyBagValue<string>(action.ToString() ?? "Null"))
           .Build(
               new PropertyBagKey(key: nameof(PageRunAndWaitForRequestFinishedOptions)),
               new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.RunAndWaitForRequestFinishedAsync(action, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task<IResponse> RunAndWaitForResponseAsync(
        Func<Task> action,
        string urlOrPredicate,
        PageRunAndWaitForResponseOptions? options = null)
    {
        _context.SessionBuilder
           .Build(
               new PropertyBagKey(key: "MethodName"),
               new PropertyBagValue<string>(nameof(RunAndWaitForResponseAsync)))
           .Build(
               new PropertyBagKey(key: nameof(action)),
               new PropertyBagValue<string>(action.ToString() ?? "Null"))
           .Build(
               new PropertyBagKey(key: nameof(urlOrPredicate)),
               new PropertyBagValue<string>(urlOrPredicate))
           .Build(
               new PropertyBagKey(key: nameof(PageRunAndWaitForResponseOptions)),
               new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.RunAndWaitForResponseAsync(action, urlOrPredicate, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task<IResponse> RunAndWaitForResponseAsync(
        Func<Task> action,
        Regex urlOrPredicate,
        PageRunAndWaitForResponseOptions? options = null)
    {
        _context.SessionBuilder
           .Build(
               new PropertyBagKey(key: "MethodName"),
               new PropertyBagValue<string>(nameof(RunAndWaitForResponseAsync)))
           .Build(
               new PropertyBagKey(key: nameof(action)),
               new PropertyBagValue<string>(action.ToString() ?? "Null"))
           .Build(
               new PropertyBagKey(key: nameof(urlOrPredicate)),
               new PropertyBagValue<string>(urlOrPredicate.ToString()))
           .Build(
               new PropertyBagKey(key: nameof(PageRunAndWaitForResponseOptions)),
               new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.RunAndWaitForResponseAsync(action, urlOrPredicate, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task<IResponse> RunAndWaitForResponseAsync(
        Func<Task> action,
        Func<IResponse, bool> urlOrPredicate,
        PageRunAndWaitForResponseOptions? options = null)
    {
        _context.SessionBuilder
           .Build(
               new PropertyBagKey(key: "MethodName"),
               new PropertyBagValue<string>(nameof(RunAndWaitForResponseAsync)))
           .Build(
               new PropertyBagKey(key: nameof(action)),
               new PropertyBagValue<string>(action.ToString() ?? "Null"))
           .Build(
               new PropertyBagKey(key: nameof(urlOrPredicate)),
               new PropertyBagValue<string>(urlOrPredicate.ToString() ?? "Null"))
           .Build(
               new PropertyBagKey(key: nameof(PageRunAndWaitForResponseOptions)),
               new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.RunAndWaitForResponseAsync(action, urlOrPredicate, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task<IWebSocket> RunAndWaitForWebSocketAsync(
        Func<Task> action,
        PageRunAndWaitForWebSocketOptions? options = null)
    {
        _context.SessionBuilder
           .Build(
               new PropertyBagKey(key: "MethodName"),
               new PropertyBagValue<string>(nameof(RunAndWaitForWebSocketAsync)))
           .Build(
               new PropertyBagKey(key: nameof(action)),
               new PropertyBagValue<string>(action.ToString() ?? "Null"))
           .Build(
               new PropertyBagKey(key: nameof(PageRunAndWaitForWebSocketOptions)),
               new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.RunAndWaitForWebSocketAsync(action, options);

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
               new PropertyBagValue<string>(nameof(RunAndWaitForWorkerAsync)))
           .Build(
               new PropertyBagKey(key: nameof(action)),
               new PropertyBagValue<string>(action.ToString() ?? "Null"))
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

    public Task<byte[]> ScreenshotAsync(PageScreenshotOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ScreenshotAsync)))
            .Build(
                new PropertyBagKey(key: nameof(PageScreenshotOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.ScreenshotAsync(options);

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
               new PropertyBagValue<string>(nameof(SelectOptionAsync)))
           .Build(
               new PropertyBagKey(key: nameof(selector)),
               new PropertyBagValue<string>(selector))
           .Build(
               new PropertyBagKey(key: nameof(values)),
               new PropertyBagValue<string>(values))
           .Build(
               new PropertyBagKey(key: nameof(PageSelectOptionOptions)),
               new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.SelectOptionAsync(selector, values, options).ConfigureAwait(false);

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
        PageSelectOptionOptions? options = null)
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
                new PropertyBagKey(key: nameof(PageSelectOptionOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.SelectOptionAsync(selector, values, options).ConfigureAwait(false);

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
        PageSelectOptionOptions? options = null)
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
                new PropertyBagKey(key: nameof(PageSelectOptionOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.SelectOptionAsync(selector, values, options).ConfigureAwait(false);

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
        PageSelectOptionOptions? options = null)
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
                new PropertyBagKey(key: nameof(PageSelectOptionOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.SelectOptionAsync(selector, values, options).ConfigureAwait(false);

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
        PageSelectOptionOptions? options = null)
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
                new PropertyBagKey(key: nameof(PageSelectOptionOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.SelectOptionAsync(selector, values, options).ConfigureAwait(false);

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
        PageSelectOptionOptions? options = null)
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
                new PropertyBagKey(key: nameof(PageSelectOptionOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.SelectOptionAsync(selector, values, options).ConfigureAwait(false);

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

    public Task SetCheckedAsync(string selector, bool checkedState, PageSetCheckedOptions? options = null)
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
                new PropertyBagKey(key: nameof(PageSetCheckedOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.SetCheckedAsync(selector, checkedState, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task SetContentAsync(string html, PageSetContentOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(SetContentAsync)))
            .Build(
                new PropertyBagKey(key: nameof(html)),
                new PropertyBagValue<string>(html))
            .Build(
                new PropertyBagKey(key: nameof(PageSetContentOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.SetContentAsync(html, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public void SetDefaultNavigationTimeout(float timeout)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(SetDefaultNavigationTimeout)))
            .Build(
                new PropertyBagKey(key: nameof(timeout)),
                new PropertyBagValue<string>(timeout.ToString(CultureInfo.InvariantCulture)));

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
                new PropertyBagValue<string>(nameof(SetDefaultTimeout)))
            .Build(
                new PropertyBagKey(key: nameof(timeout)),
                new PropertyBagValue<string>(timeout.ToString(CultureInfo.InvariantCulture)));

        _page.SetDefaultTimeout(timeout);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public Task SetExtraHTTPHeadersAsync(IEnumerable<KeyValuePair<string, string>> headers)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(SetExtraHTTPHeadersAsync)))
            .Build(
                new PropertyBagKey(key: nameof(headers)),
                new PropertyBagValue<string[]>(headers.Select(h => $"{h.Key}:{h.Value}").ToArray()));

        var result = _page.SetExtraHTTPHeadersAsync(headers);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task SetInputFilesAsync(string selector, string files, PageSetInputFilesOptions? options = null)
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
                new PropertyBagKey(key: nameof(PageSetInputFilesOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.SetInputFilesAsync(selector, files, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task SetInputFilesAsync(
        string selector,
        IEnumerable<string> files,
        PageSetInputFilesOptions? options = null)
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
                new PropertyBagKey(key: nameof(PageSetInputFilesOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.SetInputFilesAsync(selector, files, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task SetInputFilesAsync(string selector, FilePayload files, PageSetInputFilesOptions? options = null)
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
                new PropertyBagKey(key: nameof(PageSetInputFilesOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.SetInputFilesAsync(selector, files, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task SetInputFilesAsync(
        string selector,
        IEnumerable<FilePayload> files,
        PageSetInputFilesOptions? options = null)
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
                new PropertyBagKey(key: nameof(PageSetInputFilesOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.SetInputFilesAsync(selector, files, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task SetViewportSizeAsync(int width, int height)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(SetViewportSizeAsync)))
            .Build(
                new PropertyBagKey(key: nameof(width)),
                new PropertyBagValue<string>(width.ToString(CultureInfo.InvariantCulture)))
            .Build(
                new PropertyBagKey(key: nameof(height)),
                new PropertyBagValue<string>(height.ToString(CultureInfo.InvariantCulture)));

        var result = _page.SetViewportSizeAsync(width, height);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task TapAsync(string selector, PageTapOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(TapAsync)))
            .Build(
                new PropertyBagKey(key: nameof(selector)),
                new PropertyBagValue<string>(selector))
            .Build(
                new PropertyBagKey(key: nameof(PageTapOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.TapAsync(selector, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<string?> TextContentAsync(string selector, PageTextContentOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(TextContentAsync)))
            .Build(
                new PropertyBagKey(key: nameof(selector)),
                new PropertyBagValue<string>(selector))
            .Build(
                new PropertyBagKey(key: nameof(PageTextContentOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _page.TextContentAsync(selector, options).ConfigureAwait(false);

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

        var result = await _page.TitleAsync().ConfigureAwait(false);

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
    public Task TypeAsync(string selector, string text, PageTypeOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(TypeAsync)))
            .Build(
                new PropertyBagKey(key: nameof(selector)),
                new PropertyBagValue<string>(selector))
            .Build(
                new PropertyBagKey(key: nameof(text)),
                new PropertyBagValue<string>(text))
            .Build(
                new PropertyBagKey(key: nameof(PageTypeOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.TypeAsync(selector, text, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task UncheckAsync(string selector, PageUncheckOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(UncheckAsync)))
            .Build(
                new PropertyBagKey(key: nameof(selector)),
                new PropertyBagValue<string>(selector))
            .Build(
                new PropertyBagKey(key: nameof(PageUncheckOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.UncheckAsync(selector, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task UnrouteAllAsync(PageUnrouteAllOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(UnrouteAllAsync)))
            .Build(
                new PropertyBagKey(key: nameof(PageUnrouteAllOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.UnrouteAllAsync(options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task UnrouteAsync(string url, Action<IRoute>? handler = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(UnrouteAsync)))
            .Build(
                new PropertyBagKey(key: nameof(url)),
                new PropertyBagValue<string>(url))
            .Build(
                new PropertyBagKey(key: nameof(handler)),
                new PropertyBagValue<string>(handler?.ToString() ?? "Null"));

        var result = _page.UnrouteAsync(url, handler);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task UnrouteAsync(Regex url, Action<IRoute>? handler = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(UnrouteAsync)))
            .Build(
                new PropertyBagKey(key: nameof(url)),
                new PropertyBagValue<string>(url.ToString()))
            .Build(
                new PropertyBagKey(key: nameof(handler)),
                new PropertyBagValue<string>(handler?.ToString() ?? "Null"));

        var result = _page.UnrouteAsync(url, handler);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task UnrouteAsync(Func<string, bool> url, Action<IRoute>? handler = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(UnrouteAsync)))
            .Build(
                new PropertyBagKey(key: nameof(url)),
                new PropertyBagValue<string>(url.ToString() ?? "Null"))
            .Build(
                new PropertyBagKey(key: nameof(handler)),
                new PropertyBagValue<string>(handler?.ToString() ?? "Null"));

        var result = _page.UnrouteAsync(url, handler);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task UnrouteAsync(string url, Func<IRoute, Task> handler)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(UnrouteAsync)))
            .Build(
                new PropertyBagKey(key: nameof(url)),
                new PropertyBagValue<string>(url))
            .Build(
                new PropertyBagKey(key: nameof(handler)),
                new PropertyBagValue<string>(handler.ToString() ?? "Null"));

        var result = _page.UnrouteAsync(url, handler);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task UnrouteAsync(Regex url, Func<IRoute, Task> handler)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(UnrouteAsync)))
            .Build(
                new PropertyBagKey(key: nameof(url)),
                new PropertyBagValue<string>(url.ToString()))
            .Build(
                new PropertyBagKey(key: nameof(handler)),
                new PropertyBagValue<string>(handler.ToString() ?? "Null"));

        var result = _page.UnrouteAsync(url, handler);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task UnrouteAsync(Func<string, bool> url, Func<IRoute, Task> handler)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(UnrouteAsync)))
            .Build(
                new PropertyBagKey(key: nameof(url)),
                new PropertyBagValue<string>(url.ToString() ?? "Null"))
            .Build(
                new PropertyBagKey(key: nameof(handler)),
                new PropertyBagValue<string>(handler.ToString() ?? "Null"));

        var result = _page.UnrouteAsync(url, handler);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task<IConsoleMessage> WaitForConsoleMessageAsync(PageWaitForConsoleMessageOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(WaitForConsoleMessageAsync)))
            .Build(
                new PropertyBagKey(key: nameof(PageWaitForConsoleMessageOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.WaitForConsoleMessageAsync(options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task<IDownload> WaitForDownloadAsync(PageWaitForDownloadOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(WaitForDownloadAsync)))
            .Build(
                new PropertyBagKey(key: nameof(PageWaitForDownloadOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.WaitForDownloadAsync(options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task<IFileChooser> WaitForFileChooserAsync(PageWaitForFileChooserOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(WaitForFileChooserAsync)))
            .Build(
                new PropertyBagKey(key: nameof(PageWaitForFileChooserOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.WaitForFileChooserAsync(options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task<IJSHandle> WaitForFunctionAsync(
        string expression,
        object? arg = null,
        PageWaitForFunctionOptions? options = null)
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
                new PropertyBagKey(key: nameof(PageWaitForFunctionOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.WaitForFunctionAsync(expression, arg, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task WaitForLoadStateAsync(LoadState? state = null, PageWaitForLoadStateOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(WaitForLoadStateAsync)))
            .Build(
                new PropertyBagKey(key: nameof(state)),
                new PropertyBagValue<string>(state?.ToString() ?? "Null"))
            .Build(
                new PropertyBagKey(key: nameof(PageWaitForLoadStateOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.WaitForLoadStateAsync(state, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    [Obsolete("Use IPage.WaitForURLAsync instead")]
    public Task<IResponse?> WaitForNavigationAsync(PageWaitForNavigationOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(WaitForNavigationAsync)))
            .Build(
                new PropertyBagKey(key: nameof(PageWaitForNavigationOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.WaitForNavigationAsync(options);

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
                new PropertyBagValue<string>(nameof(WaitForPopupAsync)))
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

    public Task<IRequest> WaitForRequestAsync(string urlOrPredicate, PageWaitForRequestOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(WaitForRequestAsync)))
            .Build(
                new PropertyBagKey(key: nameof(urlOrPredicate)),
                new PropertyBagValue<string>(urlOrPredicate))
            .Build(
                new PropertyBagKey(key: nameof(PageWaitForRequestOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.WaitForRequestAsync(urlOrPredicate, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task<IRequest> WaitForRequestAsync(Regex urlOrPredicate, PageWaitForRequestOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(WaitForRequestAsync)))
            .Build(
                new PropertyBagKey(key: nameof(urlOrPredicate)),
                new PropertyBagValue<string>(urlOrPredicate.ToString()))
            .Build(
                new PropertyBagKey(key: nameof(PageWaitForRequestOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.WaitForRequestAsync(urlOrPredicate, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task<IRequest> WaitForRequestAsync(
        Func<IRequest, bool> urlOrPredicate,
        PageWaitForRequestOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(WaitForRequestAsync)))
            .Build(
                new PropertyBagKey(key: nameof(urlOrPredicate)),
                new PropertyBagValue<string>(urlOrPredicate.ToString() ?? "Null"))
            .Build(
                new PropertyBagKey(key: nameof(PageWaitForRequestOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.WaitForRequestAsync(urlOrPredicate, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task<IRequest> WaitForRequestFinishedAsync(PageWaitForRequestFinishedOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(WaitForRequestFinishedAsync)))
            .Build(
                new PropertyBagKey(key: nameof(PageWaitForRequestFinishedOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.WaitForRequestFinishedAsync(options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task<IResponse> WaitForResponseAsync(string urlOrPredicate, PageWaitForResponseOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(WaitForResponseAsync)))
            .Build(
                new PropertyBagKey(key: nameof(urlOrPredicate)),
                new PropertyBagValue<string>(urlOrPredicate))
            .Build(
                new PropertyBagKey(key: nameof(PageWaitForResponseOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.WaitForResponseAsync(urlOrPredicate, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task<IResponse> WaitForResponseAsync(Regex urlOrPredicate, PageWaitForResponseOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(WaitForResponseAsync)))
            .Build(
                new PropertyBagKey(key: nameof(urlOrPredicate)),
                new PropertyBagValue<string>(urlOrPredicate.ToString()))
            .Build(
                new PropertyBagKey(key: nameof(PageWaitForResponseOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.WaitForResponseAsync(urlOrPredicate, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task<IResponse> WaitForResponseAsync(
        Func<IResponse, bool> urlOrPredicate,
        PageWaitForResponseOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(WaitForResponseAsync)))
            .Build(
                new PropertyBagKey(key: nameof(urlOrPredicate)),
                new PropertyBagValue<string>(urlOrPredicate.ToString() ?? "Null"))
            .Build(
                new PropertyBagKey(key: nameof(PageWaitForResponseOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.WaitForResponseAsync(urlOrPredicate, options);

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
                new PropertyBagValue<string>(nameof(WaitForSelectorAsync)))
            .Build(
                new PropertyBagKey(key: nameof(selector)),
                new PropertyBagValue<string>(selector))
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

    public Task WaitForTimeoutAsync(float timeout)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(WaitForTimeoutAsync)))
            .Build(
                new PropertyBagKey(key: nameof(timeout)),
                new PropertyBagValue<string>(timeout.ToString(CultureInfo.InvariantCulture)));

        var result = _page.WaitForTimeoutAsync(timeout);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task WaitForURLAsync(string url, PageWaitForURLOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(WaitForURLAsync)))
            .Build(
                new PropertyBagKey(key: nameof(url)),
                new PropertyBagValue<string>(url))
            .Build(
                new PropertyBagKey(key: nameof(PageWaitForURLOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.WaitForURLAsync(url, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task WaitForURLAsync(Regex url, PageWaitForURLOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(WaitForURLAsync)))
            .Build(
                new PropertyBagKey(key: nameof(url)),
                new PropertyBagValue<string>(url.ToString()))
            .Build(
                new PropertyBagKey(key: nameof(PageWaitForURLOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.WaitForURLAsync(url, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task WaitForURLAsync(Func<string, bool> url, PageWaitForURLOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(WaitForURLAsync)))
            .Build(
                new PropertyBagKey(key: nameof(url)),
                new PropertyBagValue<string>(url.ToString() ?? "Null"))
            .Build(
                new PropertyBagKey(key: nameof(PageWaitForURLOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.WaitForURLAsync(url, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task<IWebSocket> WaitForWebSocketAsync(PageWaitForWebSocketOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(WaitForWebSocketAsync)))
            .Build(
                new PropertyBagKey(key: nameof(PageWaitForWebSocketOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.WaitForWebSocketAsync(options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task<IWorker> WaitForWorkerAsync(PageWaitForWorkerOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(WaitForWorkerAsync)))
            .Build(
                new PropertyBagKey(key: nameof(PageWaitForWorkerOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _page.WaitForWorkerAsync(options);

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
                new PropertyBagValue<string>(nameof(RequestGCAsync)));

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
                new PropertyBagValue<string>(nameof(AddLocatorHandlerAsync)))
            .Build(
                new PropertyBagKey(key: nameof(locator)),
                new PropertyBagValue<string>(locator.ToString() ?? "Null"))
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
                new PropertyBagValue<string>(nameof(RemoveLocatorHandlerAsync)))
            .Build(
                new PropertyBagKey(key: nameof(locator)),
                new PropertyBagValue<string>(locator.ToString() ?? "Null"));

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
                new PropertyBagValue<string>(nameof(RouteWebSocketAsync)))
            .Build(
                new PropertyBagKey(key: nameof(url)),
                new PropertyBagValue<string>(url))
            .Build(
                new PropertyBagKey(key: nameof(handler)),
                new PropertyBagValue<string>(handler.ToString() ?? "Null"));

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
                new PropertyBagValue<string>(nameof(RouteWebSocketAsync)))
            .Build(
                new PropertyBagKey(key: nameof(url)),
                new PropertyBagValue<string>(url.ToString()))
            .Build(
                new PropertyBagKey(key: nameof(handler)),
                new PropertyBagValue<string>(handler.ToString() ?? "Null"));

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
                new PropertyBagValue<string>(nameof(RouteWebSocketAsync)))
            .Build(
                new PropertyBagKey(key: nameof(url)),
                new PropertyBagValue<string>(url.ToString() ?? "Null"))
            .Build(
                new PropertyBagKey(key: nameof(handler)),
                new PropertyBagValue<string>(handler.ToString() ?? "Null"));

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
                new PropertyBagValue<string>(nameof(AddLocatorHandlerAsync)))
            .Build(
                new PropertyBagKey(key: nameof(locator)),
                new PropertyBagValue<string>(locator.ToString() ?? "Null"))
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
