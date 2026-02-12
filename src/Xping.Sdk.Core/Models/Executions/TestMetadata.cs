/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Collections.ObjectModel;

namespace Xping.Sdk.Core.Models.Executions;

/// <summary>
/// Immutable metadata associated with a test.
/// </summary>
public sealed class TestMetadata
{
    /// <summary>
    /// Parameterless constructor for JSON deserialization.
    /// </summary>
    public TestMetadata()
    {
        Categories = [];
        Tags = [];
        CustomAttributes = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
    }

    /// <summary>
    /// Internal constructor for manual construction.
    /// </summary>
    internal TestMetadata(
        IReadOnlyList<string> categories,
        IReadOnlyList<string> tags,
        IReadOnlyDictionary<string, string> customAttributes,
        string? description)
    {
        Categories = categories;
        Tags = tags;
        CustomAttributes = customAttributes;
        Description = description;
    }

    /// <summary>
    /// Gets the test categories (e.g., "Integration", "Unit", "E2E").
    /// </summary>
    public IReadOnlyList<string> Categories { get; private set; }

    /// <summary>
    /// Gets the test tags for filtering and organization.
    /// </summary>
    public IReadOnlyList<string> Tags { get; private set; }

    /// <summary>
    /// Gets the custom attributes for additional test metadata.
    /// </summary>
    public IReadOnlyDictionary<string, string> CustomAttributes { get; private set; }

    /// <summary>
    /// Gets the test description.
    /// </summary>
    public string? Description { get; private set; }
}
