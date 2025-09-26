/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using Microsoft.Playwright;

namespace Xping.Sdk.Validations.Content.Page.Internals.Helpers;

/// <summary>
/// Provides utility methods for formatting SelectOptionValue objects into human-readable string representations.
/// This class contains extension methods that help with logging, debugging, and displaying select option values
/// in a more understandable format.
/// </summary>
/// <remarks>
/// This static class is designed to work with Microsoft Playwright's SelectOptionValue objects,
/// providing consistent formatting across the application for better readability in logs and user interfaces.
/// </remarks>
internal static class SelectOptionValueFormatter
{
    /// <summary>
    /// Formats a SelectOptionValue in a human-readable format for logging and display purposes.
    /// </summary>
    /// <param name="selectOption">The SelectOptionValue to format.</param>
    /// <returns>A human-readable string representation of the SelectOptionValue.</returns>
    public static string FormatSelectOptionValue(this SelectOptionValue selectOption)
    {
        if (selectOption == null)
            return "null";

        var parts = new List<string>();

        if (!string.IsNullOrEmpty(selectOption.Value))
            parts.Add($"by value: '{selectOption.Value}'");

        if (!string.IsNullOrEmpty(selectOption.Label))
            parts.Add($"by label: '{selectOption.Label}'");

        if (selectOption.Index.HasValue)
            parts.Add($"by index: {selectOption.Index.Value}");

        return parts.Count > 0 ? string.Join(", ", parts) : "empty option";
    }
}
