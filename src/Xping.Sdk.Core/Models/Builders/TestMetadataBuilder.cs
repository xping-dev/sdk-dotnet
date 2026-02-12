/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Collections.ObjectModel;
using Xping.Sdk.Core.Models.Executions;

namespace Xping.Sdk.Core.Models.Builders;

/// <summary>
/// Builder for constructing immutable <see cref="TestMetadata"/> instances.
/// </summary>
public sealed class TestMetadataBuilder
{
    private readonly List<string> _categories;
    private readonly List<string> _tags;
    private readonly Dictionary<string, string> _customAttributes;
    private string? _description;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestMetadataBuilder"/> class.
    /// </summary>
    public TestMetadataBuilder()
    {
        _categories = [];
        _tags = [];
        _customAttributes = [];
    }

    /// <summary>
    /// Adds a single category.
    /// </summary>
    public TestMetadataBuilder AddCategory(string category)
    {
        if (!string.IsNullOrWhiteSpace(category))
        {
            _categories.Add(category);
        }

        return this;
    }

    /// <summary>
    /// Adds multiple categories.
    /// </summary>
    public TestMetadataBuilder AddCategories(IList<string> categories)
    {
        if (categories is { Count: > 0 })
        {
            _categories.AddRange(categories.Where(c => !string.IsNullOrWhiteSpace(c)));
        }

        return this;
    }

    /// <summary>
    /// Adds a single tag.
    /// </summary>
    public TestMetadataBuilder AddTag(string tag)
    {
        if (!string.IsNullOrWhiteSpace(tag))
        {
            _tags.Add(tag);
        }

        return this;
    }

    /// <summary>
    /// Adds multiple tags.
    /// </summary>
    public TestMetadataBuilder AddTags(IList<string> tags)
    {
        if (tags is { Count: > 0 })
        {
            _tags.AddRange(tags.Where(t => !string.IsNullOrWhiteSpace(t)));
        }

        return this;
    }

    /// <summary>
    /// Adds a custom attribute.
    /// </summary>
    public TestMetadataBuilder AddCustomAttribute(string key, string value)
    {
        if (!string.IsNullOrEmpty(key))
        {
            _customAttributes[key] = value;
        }

        return this;
    }

    /// <summary>
    /// Adds multiple custom attributes.
    /// </summary>
    public TestMetadataBuilder AddCustomAttributes(IDictionary<string, string> attributes)
    {
        if (attributes is { Count: > 0 })
        {
            foreach (var kvp in attributes)
            {
                _customAttributes[kvp.Key] = kvp.Value;
            }
        }

        return this;
    }

    /// <summary>
    /// Sets the description.
    /// </summary>
    public TestMetadataBuilder WithDescription(string? description)
    {
        _description = description;
        return this;
    }

    /// <summary>
    /// Builds an immutable <see cref="TestMetadata"/> instance.
    /// </summary>
    public TestMetadata Build()
    {
        return new TestMetadata(
            categories: _categories.AsReadOnly(),
            tags: _tags.AsReadOnly(),
            customAttributes: new ReadOnlyDictionary<string, string>(_customAttributes),
            description: _description);
    }

    /// <summary>
    /// Resets the builder to its initial state.
    /// </summary>
    public TestMetadataBuilder Reset()
    {
        _categories.Clear();
        _tags.Clear();
        _customAttributes.Clear();
        _description = null;
        return this;
    }
}
