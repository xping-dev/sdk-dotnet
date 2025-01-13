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
               new PropertyBagValue<string>(nameof(AllAsync)));

        var result = await _locator.AllAsync().ConfigureAwait(false);

        _context.SessionBuilder
           .Build(
               new PropertyBagKey(key: "Count"),
               new PropertyBagValue<int>(result.Count));

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
               new PropertyBagValue<string>(nameof(AllInnerTextsAsync)));

        var result = await _locator.AllInnerTextsAsync().ConfigureAwait(false);

        _context.SessionBuilder
           .Build(
               new PropertyBagKey(key: "InnerTexts"),
               new PropertyBagValue<string>(string.Join(";", result)));

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
               new PropertyBagValue<string>(nameof(AllTextContentsAsync)));

        var result = await _locator.AllTextContentsAsync().ConfigureAwait(false);

        _context.SessionBuilder
           .Build(
               new PropertyBagKey(key: "Count"),
               new PropertyBagValue<int>(result.Count));

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
                new PropertyBagValue<string>(nameof(And)))
            .Build(
                new PropertyBagKey(key: nameof(locator)),
                new PropertyBagValue<string>(argLocator.ToString() ?? "Null"));

        var result = _locator.And(argLocator);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public Task BlurAsync(LocatorBlurOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(BlurAsync)))
            .Build(
                new PropertyBagKey(key: nameof(LocatorBlurOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.BlurAsync(options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task<LocatorBoundingBoxResult?> BoundingBoxAsync(LocatorBoundingBoxOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(BoundingBoxAsync)))
            .Build(
                new PropertyBagKey(key: nameof(LocatorBoundingBoxOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.BoundingBoxAsync(options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task CheckAsync(LocatorCheckOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(CheckAsync)))
            .Build(
                new PropertyBagKey(key: nameof(LocatorCheckOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.CheckAsync(options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task ClearAsync(LocatorClearOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ClearAsync)))
            .Build(
                new PropertyBagKey(key: nameof(LocatorClearOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.ClearAsync(options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task ClickAsync(LocatorClickOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ClickAsync)))
            .Build(
                new PropertyBagKey(key: nameof(LocatorClickOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.ClickAsync(options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<int> CountAsync()
    {
        _context.SessionBuilder
           .Build(
               new PropertyBagKey(key: "MethodName"),
               new PropertyBagValue<string>(nameof(CountAsync)));

        var result = await _locator.CountAsync().ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Count"),
                new PropertyBagValue<int>(result));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task DblClickAsync(LocatorDblClickOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(DblClickAsync)))
            .Build(
                new PropertyBagKey(key: nameof(LocatorDblClickOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.DblClickAsync(options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task DispatchEventAsync(string type, object? eventInit = null, LocatorDispatchEventOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(DispatchEventAsync)))
            .Build(
                new PropertyBagKey(key: nameof(type)),
                new PropertyBagValue<string>(type))
            .Build(
                new PropertyBagKey(key: nameof(eventInit)),
                new PropertyBagValue<string>(eventInit?.ToString() ?? "Null"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorDispatchEventOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.DispatchEventAsync(type, eventInit, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task DragToAsync(ILocator target, LocatorDragToOptions? options = null)
    {
        var argTarget = target is InstrumentedLocator instrumented ? instrumented._locator : target;

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(DragToAsync)))
            .Build(
                new PropertyBagKey(key: nameof(LocatorDragToOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.DragToAsync(argTarget, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<IElementHandle> ElementHandleAsync(LocatorElementHandleOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ElementHandleAsync)))
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
                new PropertyBagValue<string>(nameof(ElementHandlesAsync)));

        var result = await _locator.ElementHandlesAsync().ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result.Select(h => new InstrumentedElementHandle(_context, h)).ToList().AsReadOnly();
    }

    public Task<T> EvaluateAllAsync<T>(string expression, object? arg = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(EvaluateAllAsync)))
            .Build(
                new PropertyBagKey(key: nameof(expression)),
                new PropertyBagValue<string>(expression))
            .Build(
                new PropertyBagKey(key: nameof(arg)),
                new PropertyBagValue<string>(arg?.ToString() ?? "Null"));

        var result = _locator.EvaluateAllAsync<T>(expression, arg);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task<T> EvaluateAsync<T>(string expression, object? arg = null, LocatorEvaluateOptions? options = null)
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
                new PropertyBagValue<string>(arg?.ToString() ?? "Null"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorEvaluateOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.EvaluateAsync<T>(expression, arg, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task<JsonElement?> EvaluateAsync(
        string expression,
        object? arg = null,
        LocatorEvaluateOptions? options = null)
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
                new PropertyBagValue<string>(arg?.ToString() ?? "Null"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorEvaluateOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.EvaluateAsync(expression, arg, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task<IJSHandle> EvaluateHandleAsync(
        string expression,
        object? arg = null,
        LocatorEvaluateHandleOptions? options = null)
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
                new PropertyBagValue<string>(arg?.ToString() ?? "Null"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorEvaluateHandleOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.EvaluateHandleAsync(expression, arg, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task FillAsync(string value, LocatorFillOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(FillAsync)))
            .Build(
                new PropertyBagKey(key: nameof(value)),
                new PropertyBagValue<string>(value))
            .Build(
                new PropertyBagKey(key: nameof(LocatorFillOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.FillAsync(value, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public ILocator Filter(LocatorFilterOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(Filter)))
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

    public Task FocusAsync(LocatorFocusOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(FocusAsync)))
            .Build(
                new PropertyBagKey(key: nameof(LocatorFocusOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.FocusAsync(options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
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
                new PropertyBagValue<string>(nameof(GetAttributeAsync)))
            .Build(
                new PropertyBagKey(key: nameof(name)),
                new PropertyBagValue<string>(name))
            .Build(
                new PropertyBagKey(key: nameof(LocatorGetAttributeOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _locator.GetAttributeAsync(name, options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "Attribute"),
                new PropertyBagValue<string>(result ?? "Null"));

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
                 new PropertyBagValue<string>(nameof(GetByAltText)))
            .Build(
                 new PropertyBagKey(key: nameof(text)),
                 new PropertyBagValue<string>(text))
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
                 new PropertyBagValue<string>(nameof(GetByAltText)))
            .Build(
                 new PropertyBagKey(key: nameof(text)),
                 new PropertyBagValue<string>(text.ToString()))
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
                  new PropertyBagValue<string>(nameof(GetByLabel)))
             .Build(
                  new PropertyBagKey(key: nameof(text)),
                  new PropertyBagValue<string>(text))
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
                  new PropertyBagValue<string>(nameof(GetByLabel)))
             .Build(
                  new PropertyBagKey(key: nameof(text)),
                  new PropertyBagValue<string>(text.ToString()))
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
                  new PropertyBagValue<string>(nameof(GetByPlaceholder)))
             .Build(
                  new PropertyBagKey(key: nameof(text)),
                  new PropertyBagValue<string>(text))
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
                  new PropertyBagKey(key: nameof(text)),
                  new PropertyBagValue<string>(text.ToString()))
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
                new PropertyBagValue<string>(nameof(GetByRole)))
            .Build(
                new PropertyBagKey(key: nameof(role)),
                new PropertyBagValue<string>(role.ToString()))
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

    public ILocator GetByText(string text, LocatorGetByTextOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(GetByText)))
            .Build(
                new PropertyBagKey(key: nameof(text)),
                new PropertyBagValue<string>(text))
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
                new PropertyBagValue<string>(nameof(GetByText)))
            .Build(
                new PropertyBagKey(key: nameof(text)),
                new PropertyBagValue<string>(text.ToString()))
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
                new PropertyBagValue<string>(nameof(GetByTitle)))
            .Build(
                new PropertyBagKey(key: nameof(text)),
                new PropertyBagValue<string>(text))
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
                new PropertyBagValue<string>(nameof(GetByTitle)))
            .Build(
                new PropertyBagKey(key: nameof(text)),
                new PropertyBagValue<string>(text.ToString()))
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

    public Task HighlightAsync()
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(HighlightAsync)));

        var result = _locator.HighlightAsync();

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task HoverAsync(LocatorHoverOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(HoverAsync)))
            .Build(
                new PropertyBagKey(key: nameof(LocatorHoverOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.HoverAsync(options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<string> InnerHTMLAsync(LocatorInnerHTMLOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(InnerHTMLAsync)))
            .Build(
                new PropertyBagKey(key: nameof(LocatorInnerHTMLOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _locator.InnerHTMLAsync(options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "InnerHtml"),
                new PropertyBagValue<string>(result));

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
                new PropertyBagValue<string>(nameof(InnerTextAsync)))
            .Build(
                new PropertyBagKey(key: nameof(LocatorInnerTextOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _locator.InnerTextAsync(options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "InnerText"),
                new PropertyBagValue<string>(result));

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
                new PropertyBagValue<string>(nameof(InputValueAsync)))
            .Build(
                new PropertyBagKey(key: nameof(LocatorInputValueOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _locator.InputValueAsync(options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "InputValue"),
                new PropertyBagValue<string>(result));

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
                new PropertyBagValue<string>(nameof(IsCheckedAsync)))
            .Build(
                new PropertyBagKey(key: nameof(LocatorIsCheckedOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _locator.IsCheckedAsync(options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "IsChecked"),
                new PropertyBagValue<string>(result.ToString()));

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
                new PropertyBagValue<string>(nameof(IsDisabledAsync)))
            .Build(
                new PropertyBagKey(key: nameof(LocatorIsDisabledOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _locator.IsDisabledAsync(options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "IsDisabled"),
                new PropertyBagValue<string>(result.ToString()));

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
                new PropertyBagValue<string>(nameof(IsEditableAsync)))
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
                new PropertyBagValue<string>(nameof(IsEnabledAsync)))
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
                new PropertyBagValue<string>(nameof(IsHiddenAsync)))
            .Build(
                new PropertyBagKey(key: nameof(LocatorIsHiddenOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _locator.IsHiddenAsync(options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "IsHidden"),
                new PropertyBagValue<string>(result.ToString()));

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
                new PropertyBagValue<string>(nameof(IsVisibleAsync)))
            .Build(
                new PropertyBagKey(key: nameof(LocatorIsVisibleOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _locator.IsVisibleAsync(options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "IsVisible"),
                new PropertyBagValue<string>(result.ToString()));

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
                new PropertyBagValue<string>(nameof(Locator)))
            .Build(
                new PropertyBagKey(key: nameof(selectorOrLocator)),
                new PropertyBagValue<string>(selectorOrLocator))
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
                 new PropertyBagValue<string>(nameof(Locator)))
             .Build(
                 new PropertyBagKey(key: nameof(selectorOrLocator)),
                 new PropertyBagValue<string>(argLocator.ToString() ?? "Null"))
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
                 new PropertyBagValue<string>(nameof(Nth)))
             .Build(
                 new PropertyBagKey(key: nameof(index)),
                 new PropertyBagValue<int>(index));

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
                 new PropertyBagValue<string>(nameof(Or)))
             .Build(
                 new PropertyBagKey(key: nameof(locator)),
                 new PropertyBagValue<string>(argLocator.ToString() ?? "Null"));

        var result = _locator.Or(argLocator);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return new InstrumentedLocator(_context, result);
    }

    public Task PressAsync(string key, LocatorPressOptions? options = null)
    {
        _context.SessionBuilder
             .Build(
                 new PropertyBagKey(key: "MethodName"),
                 new PropertyBagValue<string>(nameof(PressAsync)))
             .Build(
                 new PropertyBagKey(key: nameof(key)),
                 new PropertyBagValue<string>(key))
             .Build(
                 new PropertyBagKey(key: nameof(LocatorPressOptions)),
                 new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.PressAsync(key, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task PressSequentiallyAsync(string text, LocatorPressSequentiallyOptions? options = null)
    {
        _context.SessionBuilder
             .Build(
                 new PropertyBagKey(key: "MethodName"),
                 new PropertyBagValue<string>(nameof(PressSequentiallyAsync)))
             .Build(
                 new PropertyBagKey(key: nameof(text)),
                 new PropertyBagValue<string>(text))
             .Build(
                 new PropertyBagKey(key: nameof(LocatorPressSequentiallyOptions)),
                 new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.PressSequentiallyAsync(text, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task<byte[]> ScreenshotAsync(LocatorScreenshotOptions? options = null)
    {
        _context.SessionBuilder
             .Build(
                 new PropertyBagKey(key: "MethodName"),
                 new PropertyBagValue<string>(nameof(ScreenshotAsync)))
             .Build(
                 new PropertyBagKey(key: nameof(LocatorScreenshotOptions)),
                 new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.ScreenshotAsync(options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task ScrollIntoViewIfNeededAsync(LocatorScrollIntoViewIfNeededOptions? options = null)
    {
        _context.SessionBuilder
             .Build(
                 new PropertyBagKey(key: "MethodName"),
                 new PropertyBagValue<string>(nameof(ScrollIntoViewIfNeededAsync)))
             .Build(
                 new PropertyBagKey(key: nameof(LocatorScrollIntoViewIfNeededOptions)),
                 new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.ScrollIntoViewIfNeededAsync(options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<IReadOnlyList<string>> SelectOptionAsync(
        string values,
        LocatorSelectOptionOptions? options = null)
    {
        _context.SessionBuilder
             .Build(
                 new PropertyBagKey(key: "MethodName"),
                 new PropertyBagValue<string>(nameof(SelectOptionAsync)))
             .Build(
                 new PropertyBagKey(key: nameof(values)),
                 new PropertyBagValue<string>(values))
             .Build(
                 new PropertyBagKey(key: nameof(LocatorSelectOptionOptions)),
                 new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _locator.SelectOptionAsync(values, options).ConfigureAwait(false);

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
        IElementHandle values,
        LocatorSelectOptionOptions? options = null)
    {
        var argHandle = values is InstrumentedElementHandle instrumented ? instrumented.ElementHandle : values;

        _context.SessionBuilder
             .Build(
                 new PropertyBagKey(key: "MethodName"),
                 new PropertyBagValue<string>(nameof(SelectOptionAsync)))
             .Build(
                 new PropertyBagKey(key: nameof(values)),
                 new PropertyBagValue<string>(argHandle.ToString() ?? "Null"))
             .Build(
                 new PropertyBagKey(key: nameof(LocatorSelectOptionOptions)),
                 new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _locator.SelectOptionAsync(argHandle, options).ConfigureAwait(false);

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
        IEnumerable<string> values,
        LocatorSelectOptionOptions? options = null)
    {
        var enumerable = values as string[] ?? values.ToArray();
        _context.SessionBuilder
             .Build(
                 new PropertyBagKey(key: "MethodName"),
                 new PropertyBagValue<string>(nameof(SelectOptionAsync)))
             .Build(
                 new PropertyBagKey(key: nameof(values)),
                 new PropertyBagValue<string[]>([.. enumerable]))
             .Build(
                 new PropertyBagKey(key: nameof(LocatorSelectOptionOptions)),
                 new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _locator.SelectOptionAsync(enumerable, options).ConfigureAwait(false);

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
        SelectOptionValue values,
        LocatorSelectOptionOptions? options = null)
    {
        _context.SessionBuilder
             .Build(
                 new PropertyBagKey(key: "MethodName"),
                 new PropertyBagValue<string>(nameof(SelectOptionAsync)))
             .Build(
                 new PropertyBagKey(key: nameof(values)),
                 new PropertyBagValue<string>(JsonSerializer.Serialize(values)))
             .Build(
                 new PropertyBagKey(key: nameof(LocatorSelectOptionOptions)),
                 new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _locator.SelectOptionAsync(values, options).ConfigureAwait(false);

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
                 new PropertyBagValue<string>(nameof(SelectOptionAsync)))
             .Build(
                 new PropertyBagKey(key: nameof(values)),
                 new PropertyBagValue<string[]>(elementHandles.Select(v => v.ToString() ?? "Null").ToArray()))
             .Build(
                 new PropertyBagKey(key: nameof(LocatorSelectOptionOptions)),
                 new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _locator.SelectOptionAsync(elementHandles, options).ConfigureAwait(false);

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
        IEnumerable<SelectOptionValue> values,
        LocatorSelectOptionOptions? options = null)
    {
        var selectOptionValues = values as SelectOptionValue[] ?? values.ToArray();
        _context.SessionBuilder
             .Build(
                 new PropertyBagKey(key: "MethodName"),
                 new PropertyBagValue<string>(nameof(SelectOptionAsync)))
             .Build(
                 new PropertyBagKey(key: nameof(values)),
                 new PropertyBagValue<string[]>(selectOptionValues.Select(v => JsonSerializer.Serialize(v)).ToArray()))
             .Build(
                 new PropertyBagKey(key: nameof(LocatorSelectOptionOptions)),
                 new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _locator.SelectOptionAsync(selectOptionValues, options).ConfigureAwait(false);

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

    public Task SelectTextAsync(LocatorSelectTextOptions? options = null)
    {
        _context.SessionBuilder
             .Build(
                 new PropertyBagKey(key: "MethodName"),
                 new PropertyBagValue<string>(nameof(SelectTextAsync)))
             .Build(
                 new PropertyBagKey(key: nameof(LocatorSelectTextOptions)),
                 new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.SelectTextAsync(options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task SetCheckedAsync(bool checkedState, LocatorSetCheckedOptions? options = null)
    {
        _context.SessionBuilder
             .Build(
                 new PropertyBagKey(key: "MethodName"),
                 new PropertyBagValue<string>(nameof(SetCheckedAsync)))
             .Build(
                 new PropertyBagKey(key: nameof(checkedState)),
                 new PropertyBagValue<string>(checkedState.ToString()))
             .Build(
                 new PropertyBagKey(key: nameof(LocatorSetCheckedOptions)),
                 new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.SetCheckedAsync(checkedState, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task SetInputFilesAsync(string files, LocatorSetInputFilesOptions? options = null)
    {
        _context.SessionBuilder
             .Build(
                 new PropertyBagKey(key: "MethodName"),
                 new PropertyBagValue<string>(nameof(SetInputFilesAsync)))
             .Build(
                 new PropertyBagKey(key: nameof(files)),
                 new PropertyBagValue<string>(files))
             .Build(
                 new PropertyBagKey(key: nameof(LocatorSetInputFilesOptions)),
                 new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.SetInputFilesAsync(files, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task SetInputFilesAsync(IEnumerable<string> files, LocatorSetInputFilesOptions? options = null)
    {
        var enumerable = files as string[] ?? files.ToArray();
        _context.SessionBuilder
             .Build(
                 new PropertyBagKey(key: "MethodName"),
                 new PropertyBagValue<string>(nameof(SetInputFilesAsync)))
             .Build(
                 new PropertyBagKey(key: nameof(files)),
                 new PropertyBagValue<string[]>([.. enumerable]))
             .Build(
                 new PropertyBagKey(key: nameof(LocatorSetInputFilesOptions)),
                 new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.SetInputFilesAsync(enumerable, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task SetInputFilesAsync(FilePayload files, LocatorSetInputFilesOptions? options = null)
    {
        _context.SessionBuilder
             .Build(
                 new PropertyBagKey(key: "MethodName"),
                 new PropertyBagValue<string>(nameof(SetInputFilesAsync)))
             .Build(
                 new PropertyBagKey(key: nameof(files)),
                 new PropertyBagValue<string>($"Name: {files.Name}; MimeType: {files.MimeType}"))
             .Build(
                 new PropertyBagKey(key: nameof(LocatorSetInputFilesOptions)),
                 new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.SetInputFilesAsync(files, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task SetInputFilesAsync(IEnumerable<FilePayload> files, LocatorSetInputFilesOptions? options = null)
    {
        var filePayloads = files as FilePayload[] ?? files.ToArray();
        _context.SessionBuilder
             .Build(
                 new PropertyBagKey(key: "MethodName"),
                 new PropertyBagValue<string>(nameof(SetInputFilesAsync)))
             .Build(
                 new PropertyBagKey(key: nameof(files)),
                 new PropertyBagValue<string[]>(
                     filePayloads.Select(f => $"Name: {f.Name}; MimeType: {f.MimeType}").ToArray()))
             .Build(
                 new PropertyBagKey(key: nameof(LocatorSetInputFilesOptions)),
                 new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.SetInputFilesAsync(filePayloads, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task TapAsync(LocatorTapOptions? options = null)
    {
        _context.SessionBuilder
             .Build(
                 new PropertyBagKey(key: "MethodName"),
                 new PropertyBagValue<string>(nameof(TapAsync)))
             .Build(
                 new PropertyBagKey(key: nameof(LocatorTapOptions)),
                 new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.TapAsync(options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<string?> TextContentAsync(LocatorTextContentOptions? options = null)
    {
        _context.SessionBuilder
             .Build(
                 new PropertyBagKey(key: "MethodName"),
                 new PropertyBagValue<string>(nameof(TextContentAsync)))
             .Build(
                 new PropertyBagKey(key: nameof(LocatorTextContentOptions)),
                 new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _locator.TextContentAsync(options).ConfigureAwait(false);

        _context.SessionBuilder
             .Build(
                 new PropertyBagKey(key: "TextContent"),
                 new PropertyBagValue<string>(result ?? "Null"));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    [Obsolete($"Use {nameof(FillAsync)} instead.")]
    public Task TypeAsync(string text, LocatorTypeOptions? options = null)
    {
        _context.SessionBuilder
             .Build(
                 new PropertyBagKey(key: "MethodName"),
                 new PropertyBagValue<string>(nameof(TypeAsync)))
             .Build(
                 new PropertyBagKey(key: nameof(text)),
                 new PropertyBagValue<string>(text))
             .Build(
                 new PropertyBagKey(key: nameof(LocatorTypeOptions)),
                 new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.TypeAsync(text, options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task UncheckAsync(LocatorUncheckOptions? options = null)
    {
        _context.SessionBuilder
             .Build(
                 new PropertyBagKey(key: "MethodName"),
                 new PropertyBagValue<string>(nameof(UncheckAsync)))
             .Build(
                 new PropertyBagKey(key: nameof(LocatorUncheckOptions)),
                 new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.UncheckAsync(options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public Task WaitForAsync(LocatorWaitForOptions? options = null)
    {
        _context.SessionBuilder
             .Build(
                 new PropertyBagKey(key: "MethodName"),
                 new PropertyBagValue<string>(nameof(WaitForAsync)))
             .Build(
                 new PropertyBagKey(key: nameof(LocatorWaitForOptions)),
                 new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = _locator.WaitForAsync(options);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }

    public async Task<string> AriaSnapshotAsync(LocatorAriaSnapshotOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(AriaSnapshotAsync)))
            .Build(
                new PropertyBagKey(key: nameof(LocatorAriaSnapshotOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options)));

        var result = await _locator.AriaSnapshotAsync(options).ConfigureAwait(false);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "AriaSnapshot"),
                new PropertyBagValue<string>(result));

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);

        return result;
    }
}
