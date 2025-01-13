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

internal class InstrumentedPageAssertions(TestContext context, IPageAssertions pageAssertions) : IPageAssertions
{
    private readonly TestContext _context = context.RequireNotNull(nameof(context));
    private readonly IPageAssertions _pageAssertions = pageAssertions.RequireNotNull(nameof(pageAssertions));

    public IPageAssertions Not => new InstrumentedPageAssertions(_context, _pageAssertions.Not);

    public async Task ToHaveTitleAsync(string titleOrRegExp, PageAssertionsToHaveTitleOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToHaveTitleAsync)))
            .Build(
                new PropertyBagKey(key: nameof(titleOrRegExp)),
                new PropertyBagValue<string>(titleOrRegExp))
            .Build(
                new PropertyBagKey(key: nameof(PageAssertionsToHaveTitleOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options))
            );

        await _pageAssertions.ToHaveTitleAsync(titleOrRegExp, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task ToHaveTitleAsync(Regex titleOrRegExp, PageAssertionsToHaveTitleOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToHaveTitleAsync)))
            .Build(
                new PropertyBagKey(key: nameof(titleOrRegExp)),
                new PropertyBagValue<string>(titleOrRegExp.ToString()))
            .Build(
                new PropertyBagKey(key: nameof(PageAssertionsToHaveTitleOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options))
            );

        await _pageAssertions.ToHaveTitleAsync(titleOrRegExp, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task ToHaveURLAsync(string urlOrRegExp, PageAssertionsToHaveURLOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToHaveURLAsync)))
            .Build(
                new PropertyBagKey(key: nameof(urlOrRegExp)),
                new PropertyBagValue<string>(urlOrRegExp))
            .Build(
                new PropertyBagKey(key: nameof(PageAssertionsToHaveURLOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options))
            );

        await _pageAssertions.ToHaveURLAsync(urlOrRegExp, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }

    public async Task ToHaveURLAsync(Regex urlOrRegExp, PageAssertionsToHaveURLOptions? options = null)
    {
        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>(nameof(ToHaveURLAsync)))
            .Build(
                new PropertyBagKey(key: nameof(urlOrRegExp)),
                new PropertyBagValue<string>(urlOrRegExp.ToString()))
            .Build(
                new PropertyBagKey(key: nameof(PageAssertionsToHaveURLOptions)),
                new PropertyBagValue<string>(JsonSerializer.Serialize(options))
            );

        await _pageAssertions.ToHaveURLAsync(urlOrRegExp, options).ConfigureAwait(false);

        // Create a successful test step with information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }
}
