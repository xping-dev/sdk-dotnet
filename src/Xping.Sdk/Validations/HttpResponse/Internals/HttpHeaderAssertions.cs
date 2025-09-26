/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using Xping.Sdk.Core.Common;
using Xping.Sdk.Core.Components;
using Xping.Sdk.Shared;
using Xping.Sdk.Validations.Content.Html;
using Xping.Sdk.Validations.TextUtils;
using Xping.Sdk.Validations.TextUtils.Internals;

namespace Xping.Sdk.Validations.HttpResponse.Internals;

internal class HttpHeaderAssertions(
    string header, IEnumerable<string> values, TestContext context)
{
    private readonly string _header = header.RequireNotNull(nameof(header));
    private readonly IEnumerable<string> _values = values.RequireNotNull(nameof(values));
    private readonly TestContext _context = context.RequireNotNull(nameof(context));

    public void WithValue(string value, TextOptions? options = null)
    {
        var normalizedValue = value.Trim();
        var textComparer = new TextComparer(options);

        _context.SessionBuilder
            .Build(
                new PropertyBagKey(key: "MethodName"),
                new PropertyBagValue<string>($"{nameof(HttpHeaderAssertions)}.{nameof(WithValue)}"))
            .Build(
                new PropertyBagKey(key: "DisplayName"),
                new PropertyBagValue<string>($"Validating HTTP header \"{_header}\" contains value \"{normalizedValue}\""))
            .Build(
                new PropertyBagKey(key: nameof(TextOptions)),
                new PropertyBagValue<string>(options?.ToString() ?? "Null"));

        if (!_values.Any(v => textComparer.Compare(v, normalizedValue)))
        {
            throw new ValidationException(
                $"Expected to find HTTP header \"{_header}\" with value \"{normalizedValue}\", but the actual value" +
                $"was \"{string.Join(";", _values)}\".");
        }

        // Create a successful test step with detailed information about the current test operation.
        var testStep = _context.SessionBuilder.Build();
        // Report the progress of this test step.
        _context.Progress?.Report(testStep);
    }
}
