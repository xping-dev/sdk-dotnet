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

internal class InstrumentedLocatorAssertions(TestContext context, ILocatorAssertions locatorAssertions)
    : ILocatorAssertions
{
    private readonly TestContext _context = context.RequireNotNull(nameof(context));
    private readonly ILocatorAssertions
        _locatorAssertions = locatorAssertions.RequireNotNull(nameof(locatorAssertions));

    public ILocatorAssertions Not => new InstrumentedLocatorAssertions(_context, _locatorAssertions.Not);
    
    public async Task ToBeAttachedAsync(LocatorAssertionsToBeAttachedOptions? options)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToBeAttachedAsync)))
            .Build(
                new PropertyBagKey(key: nameof(LocatorAssertionsToBeAttachedOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options))
            );

        await _locatorAssertions.ToBeAttachedAsync(options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task ToBeCheckedAsync(LocatorAssertionsToBeCheckedOptions? options)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToBeCheckedAsync)))
            .Build(
                new PropertyBagKey(key: nameof(LocatorAssertionsToBeCheckedOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options))
            );

        await _locatorAssertions.ToBeCheckedAsync(options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task ToBeDisabledAsync(LocatorAssertionsToBeDisabledOptions? options)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToBeDisabledAsync)))
            .Build(
                new PropertyBagKey(key: nameof(LocatorAssertionsToBeDisabledOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options))
            );

        await _locatorAssertions.ToBeDisabledAsync(options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task ToBeEditableAsync(LocatorAssertionsToBeEditableOptions? options)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToBeEditableAsync)))
            .Build(
                new PropertyBagKey(key: nameof(LocatorAssertionsToBeEditableOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options))
            );

        await _locatorAssertions.ToBeEditableAsync(options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task ToBeEmptyAsync(LocatorAssertionsToBeEmptyOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToBeEmptyAsync)))
            .Build(
                new PropertyBagKey(key: nameof(LocatorAssertionsToBeEmptyOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options))
            );

        await _locatorAssertions.ToBeEmptyAsync(options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task ToBeEnabledAsync(LocatorAssertionsToBeEnabledOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToBeEnabledAsync)))
            .Build(
                new PropertyBagKey(key: nameof(LocatorAssertionsToBeEnabledOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options))
            );

        await _locatorAssertions.ToBeEnabledAsync(options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task ToBeFocusedAsync(LocatorAssertionsToBeFocusedOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToBeFocusedAsync)))
            .Build(
                new PropertyBagKey(key: nameof(LocatorAssertionsToBeFocusedOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options))
            );

        await _locatorAssertions.ToBeFocusedAsync(options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task ToBeHiddenAsync(LocatorAssertionsToBeHiddenOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToBeHiddenAsync)))
            .Build(
                new PropertyBagKey(key: nameof(LocatorAssertionsToBeHiddenOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options))
            );

        await _locatorAssertions.ToBeHiddenAsync(options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task ToBeInViewportAsync(LocatorAssertionsToBeInViewportOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToBeInViewportAsync)))
            .Build(
                new PropertyBagKey(key: nameof(LocatorAssertionsToBeInViewportOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options))
            );

        await _locatorAssertions.ToBeInViewportAsync(options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task ToBeVisibleAsync(LocatorAssertionsToBeVisibleOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToBeVisibleAsync)))
            .Build(
                new PropertyBagKey(key: nameof(LocatorAssertionsToBeVisibleOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options))
            );

        await _locatorAssertions.ToBeVisibleAsync(options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task ToContainTextAsync(string expected, LocatorAssertionsToContainTextOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToContainTextAsync)))
            .Build(
                new PropertyBagKey(key: nameof(expected)),
                new PropertyBagValue<string>(expected))
            .Build(
                new PropertyBagKey(key: nameof(LocatorAssertionsToContainTextOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options))
            );

        await _locatorAssertions.ToContainTextAsync(expected, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task ToContainTextAsync(Regex expected, LocatorAssertionsToContainTextOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToContainTextAsync)))
            .Build(
                new PropertyBagKey(key: nameof(expected)),
                new PropertyBagValue<string>(expected.ToString()))
            .Build(
                new PropertyBagKey(key: nameof(LocatorAssertionsToContainTextOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options))
            );

        await _locatorAssertions.ToContainTextAsync(expected, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task ToContainTextAsync(
        IEnumerable<string> expected, LocatorAssertionsToContainTextOptions? options = null)
    {
        var values = expected as string[] ?? expected.ToArray();
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToContainTextAsync)))
            .Build(
                new PropertyBagKey(key: nameof(expected)),
                new PropertyBagValue<string[]>(values))
            .Build(
                new PropertyBagKey(key: nameof(LocatorAssertionsToContainTextOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options))
            );

        await _locatorAssertions.ToContainTextAsync(values, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task ToContainTextAsync(
        IEnumerable<Regex> expected, LocatorAssertionsToContainTextOptions? options = null)
    {
        var values = expected as Regex[] ?? expected.ToArray();
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToContainTextAsync)))
            .Build(
                new PropertyBagKey(key: nameof(expected)),
                new PropertyBagValue<string[]>(values.Select(v => v.ToString()).ToArray()))
            .Build(
                new PropertyBagKey(key: nameof(LocatorAssertionsToContainTextOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options))
            );

        await _locatorAssertions.ToContainTextAsync(values, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task ToHaveAccessibleDescriptionAsync(
        string description, LocatorAssertionsToHaveAccessibleDescriptionOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToHaveAccessibleDescriptionAsync)))
            .Build(
                new PropertyBagKey(key: nameof(description)),
                new PropertyBagValue<string>(description))
            .Build(
                new PropertyBagKey(key: nameof(LocatorAssertionsToHaveAccessibleDescriptionOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options))
            );

        await _locatorAssertions.ToHaveAccessibleDescriptionAsync(description, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task ToHaveAccessibleDescriptionAsync(
        Regex description, LocatorAssertionsToHaveAccessibleDescriptionOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToHaveAccessibleDescriptionAsync)))
            .Build(
                new PropertyBagKey(key: nameof(description)),
                new PropertyBagValue<string>(description.ToString()))
            .Build(
                new PropertyBagKey(key: nameof(LocatorAssertionsToHaveAccessibleDescriptionOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options))
            );

        await _locatorAssertions.ToHaveAccessibleDescriptionAsync(description, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task ToHaveAccessibleNameAsync(
        string name, LocatorAssertionsToHaveAccessibleNameOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToHaveAccessibleNameAsync)))
            .Build(
                new PropertyBagKey(key: nameof(name)),
                new PropertyBagValue<string>(name))
            .Build(
                new PropertyBagKey(key: nameof(LocatorAssertionsToHaveAccessibleNameOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options))
            );

        await _locatorAssertions.ToHaveAccessibleNameAsync(name, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task ToHaveAccessibleNameAsync(
        Regex name, LocatorAssertionsToHaveAccessibleNameOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToHaveAccessibleNameAsync)))
            .Build(
                new PropertyBagKey(key: nameof(name)),
                new PropertyBagValue<string>(name.ToString()))
            .Build(
                new PropertyBagKey(key: nameof(LocatorAssertionsToHaveAccessibleNameOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options))
            );

        await _locatorAssertions.ToHaveAccessibleNameAsync(name, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task ToHaveAttributeAsync(
        string name, string value, LocatorAssertionsToHaveAttributeOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToHaveAttributeAsync)))
            .Build(
                new PropertyBagKey(key: nameof(name)),
                new PropertyBagValue<string>(name))
            .Build(
                new PropertyBagKey(key: nameof(value)),
                new PropertyBagValue<string>(value))
            .Build(
                new PropertyBagKey(key: nameof(LocatorAssertionsToHaveAttributeOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options))
            );

        await _locatorAssertions.ToHaveAttributeAsync(name, value, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task ToHaveAttributeAsync(string name, Regex value, LocatorAssertionsToHaveAttributeOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToHaveAttributeAsync)))
            .Build(
                new PropertyBagKey(key: nameof(name)),
                new PropertyBagValue<string>(name))
            .Build(
                new PropertyBagKey(key: nameof(value)),
                new PropertyBagValue<string>(value.ToString()))
            .Build(
                new PropertyBagKey(key: nameof(LocatorAssertionsToHaveAttributeOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options))
            );

        await _locatorAssertions.ToHaveAttributeAsync(name, value, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task ToHaveClassAsync(string expected, LocatorAssertionsToHaveClassOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToHaveClassAsync)))
            .Build(
                new PropertyBagKey(key: nameof(expected)),
                new PropertyBagValue<string>(expected))
            .Build(
                new PropertyBagKey(key: nameof(LocatorAssertionsToHaveClassOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options))
            );

        await _locatorAssertions.ToHaveClassAsync(expected, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task ToHaveClassAsync(Regex expected, LocatorAssertionsToHaveClassOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToHaveClassAsync)))
            .Build(
                new PropertyBagKey(key: nameof(expected)),
                new PropertyBagValue<string>(expected.ToString()))
            .Build(
                new PropertyBagKey(key: nameof(LocatorAssertionsToHaveClassOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options))
            );

        await _locatorAssertions.ToHaveClassAsync(expected, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task ToHaveClassAsync(IEnumerable<string> expected, LocatorAssertionsToHaveClassOptions? options = null)
    {
        var values = expected as string[] ?? expected.ToArray();
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToHaveClassAsync)))
            .Build(
                new PropertyBagKey(key: nameof(expected)),
                new PropertyBagValue<string[]>(values))
            .Build(
                new PropertyBagKey(key: nameof(LocatorAssertionsToHaveClassOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options))
            );

        await _locatorAssertions.ToHaveClassAsync(values, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task ToHaveClassAsync(IEnumerable<Regex> expected, LocatorAssertionsToHaveClassOptions? options = null)
    {
        var values = expected as Regex[] ?? expected.ToArray();
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToHaveClassAsync)))
            .Build(
                new PropertyBagKey(key: nameof(expected)),
                new PropertyBagValue<string[]>(values.Select(v => v.ToString()).ToArray()))
            .Build(
                new PropertyBagKey(key: nameof(LocatorAssertionsToHaveClassOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options))
            );

        await _locatorAssertions.ToHaveClassAsync(values, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task ToHaveCountAsync(int count, LocatorAssertionsToHaveCountOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToHaveCountAsync)))
            .Build(
                new PropertyBagKey(key: nameof(count)),
                new PropertyBagValue<string>(count.ToString(CultureInfo.InvariantCulture)))
            .Build(
                new PropertyBagKey(key: nameof(LocatorAssertionsToHaveCountOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options))
            );

        await _locatorAssertions.ToHaveCountAsync(count, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task ToHaveCSSAsync(string name, string value, LocatorAssertionsToHaveCSSOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToHaveCSSAsync)))
            .Build(
                new PropertyBagKey(key: nameof(name)),
                new PropertyBagValue<string>(name))
            .Build(
                new PropertyBagKey(key: nameof(value)),
                new PropertyBagValue<string>(value))
            .Build(
                new PropertyBagKey(key: nameof(LocatorAssertionsToHaveCSSOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options))
            );

        await _locatorAssertions.ToHaveCSSAsync(name, value, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task ToHaveCSSAsync(string name, Regex value, LocatorAssertionsToHaveCSSOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToHaveCSSAsync)))
            .Build(
                new PropertyBagKey(key: nameof(name)),
                new PropertyBagValue<string>(name))
            .Build(
                new PropertyBagKey(key: nameof(value)),
                new PropertyBagValue<string>(value.ToString()))
            .Build(
                new PropertyBagKey(key: nameof(LocatorAssertionsToHaveCSSOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options))
            );

        await _locatorAssertions.ToHaveCSSAsync(name, value, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task ToHaveIdAsync(string id, LocatorAssertionsToHaveIdOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToHaveIdAsync)))
            .Build(
                new PropertyBagKey(key: nameof(id)),
                new PropertyBagValue<string>(id))
            .Build(
                new PropertyBagKey(key: nameof(LocatorAssertionsToHaveIdOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options))
            );

        await _locatorAssertions.ToHaveIdAsync(id, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task ToHaveIdAsync(Regex id, LocatorAssertionsToHaveIdOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToHaveIdAsync)))
            .Build(
                new PropertyBagKey(key: nameof(id)),
                new PropertyBagValue<string>(id.ToString()))
            .Build(
                new PropertyBagKey(key: nameof(LocatorAssertionsToHaveIdOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options))
            );

        await _locatorAssertions.ToHaveIdAsync(id, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task ToHaveJSPropertyAsync(
        string name, object value, LocatorAssertionsToHaveJSPropertyOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToHaveJSPropertyAsync)))
            .Build(
                new PropertyBagKey(key: nameof(name)),
                new PropertyBagValue<string>(name))
            .Build(
                new PropertyBagKey(key: nameof(value)),
                new PropertyBagValue<string>(value.ToString() ?? "Null"))
            .Build(
                new PropertyBagKey(key: nameof(LocatorAssertionsToHaveJSPropertyOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options))
            );

        await _locatorAssertions.ToHaveJSPropertyAsync(name, value, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task ToHaveRoleAsync(AriaRole role, LocatorAssertionsToHaveRoleOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToHaveRoleAsync)))
            .Build(
                new PropertyBagKey(key: nameof(role)),
                new PropertyBagValue<string>(role.ToString()))
            .Build(
                new PropertyBagKey(key: nameof(LocatorAssertionsToHaveRoleOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options))
            );

        await _locatorAssertions.ToHaveRoleAsync(role, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task ToHaveTextAsync(string expected, LocatorAssertionsToHaveTextOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToHaveTextAsync)))
            .Build(
                new PropertyBagKey(key: nameof(expected)),
                new PropertyBagValue<string>(expected))
            .Build(
                new PropertyBagKey(key: nameof(LocatorAssertionsToHaveTextOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options))
            );

        await _locatorAssertions.ToHaveTextAsync(expected, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task ToHaveTextAsync(Regex expected, LocatorAssertionsToHaveTextOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToHaveTextAsync)))
            .Build(
                new PropertyBagKey(key: nameof(expected)),
                new PropertyBagValue<string>(expected.ToString()))
            .Build(
                new PropertyBagKey(key: nameof(LocatorAssertionsToHaveTextOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options))
            );

        await _locatorAssertions.ToHaveTextAsync(expected, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task ToHaveTextAsync(IEnumerable<string> expected, LocatorAssertionsToHaveTextOptions? options = null)
    {
        var values = expected as string[] ?? expected.ToArray();
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToHaveTextAsync)))
            .Build(
                new PropertyBagKey(key: nameof(expected)),
                new PropertyBagValue<string[]>(values))
            .Build(
                new PropertyBagKey(key: nameof(LocatorAssertionsToHaveTextOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options))
            );

        await _locatorAssertions.ToHaveTextAsync(values, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task ToHaveTextAsync(IEnumerable<Regex> expected, LocatorAssertionsToHaveTextOptions? options = null)
    {
        var values = expected as Regex[] ?? expected.ToArray();
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToHaveTextAsync)))
            .Build(
                new PropertyBagKey(key: nameof(expected)),
                new PropertyBagValue<string[]>(values.Select(v => v.ToString()).ToArray()))
            .Build(
                new PropertyBagKey(key: nameof(LocatorAssertionsToHaveTextOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options))
            );

        await _locatorAssertions.ToHaveTextAsync(values, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task ToHaveValueAsync(string value, LocatorAssertionsToHaveValueOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToHaveValueAsync)))
            .Build(
                new PropertyBagKey(key: nameof(value)),
                new PropertyBagValue<string>(value))
            .Build(
                new PropertyBagKey(key: nameof(LocatorAssertionsToHaveValueOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options))
            );

        await _locatorAssertions.ToHaveValueAsync(value, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task ToHaveValueAsync(Regex value, LocatorAssertionsToHaveValueOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToHaveValueAsync)))
            .Build(
                new PropertyBagKey(key: nameof(value)),
                new PropertyBagValue<string>(value.ToString()))
            .Build(
                new PropertyBagKey(key: nameof(LocatorAssertionsToHaveValueOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options))
            );

        await _locatorAssertions.ToHaveValueAsync(value, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task ToHaveValuesAsync(IEnumerable<string> values, LocatorAssertionsToHaveValuesOptions? options = null)
    {
        var enumerable = values as string[] ?? values.ToArray();
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToHaveValuesAsync)))
            .Build(
                new PropertyBagKey(key: nameof(values)),
                new PropertyBagValue<string[]>(enumerable))
            .Build(
                new PropertyBagKey(key: nameof(LocatorAssertionsToHaveValuesOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options))
            );

        await _locatorAssertions.ToHaveValuesAsync(enumerable, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task ToHaveValuesAsync(IEnumerable<Regex> values, LocatorAssertionsToHaveValuesOptions? options = null)
    {
        var enumerable = values as Regex[] ?? values.ToArray();
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToHaveValuesAsync)))
            .Build(
                new PropertyBagKey(key: nameof(values)),
                new PropertyBagValue<string[]>(enumerable.Select(e => e.ToString()).ToArray()))
            .Build(
                new PropertyBagKey(key: nameof(LocatorAssertionsToHaveValuesOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options))
            );

        await _locatorAssertions.ToHaveValuesAsync(enumerable, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task ToMatchAriaSnapshotAsync(
        string expected, LocatorAssertionsToMatchAriaSnapshotOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToMatchAriaSnapshotAsync)))
            .Build(
                new PropertyBagKey(key: nameof(expected)),
                new PropertyBagValue<string>(expected))
            .Build(
                new PropertyBagKey(key: nameof(LocatorAssertionsToMatchAriaSnapshotOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options))
            );

        await _locatorAssertions.ToMatchAriaSnapshotAsync(expected, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }
}