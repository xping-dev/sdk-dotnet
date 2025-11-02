/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Models;

using System;
using System.Collections.Generic;

/// <summary>
/// Represents metadata associated with a test.
/// </summary>
public sealed class TestMetadata
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestMetadata"/> class.
    /// </summary>
    public TestMetadata()
    {
        Categories = Array.Empty<string>();
        Tags = Array.Empty<string>();
        CustomAttributes = new Dictionary<string, string>();
    }

    /// <summary>
    /// Gets or sets the test categories (e.g., "Integration", "Unit", "E2E").
    /// </summary>
    public IReadOnlyList<string> Categories { get; set; }

    /// <summary>
    /// Gets or sets the test tags for filtering and organization.
    /// </summary>
    public IReadOnlyList<string> Tags { get; set; }

    /// <summary>
    /// Gets the custom attributes for additional test metadata.
    /// </summary>
    public Dictionary<string, string> CustomAttributes { get; }

    /// <summary>
    /// Gets or sets the test description.
    /// </summary>
    public string Description { get; set; }
}

