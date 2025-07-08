/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

namespace Xping.Sdk.Core;

/// <summary>
/// Specifies a unique identifier for test session mapping.
/// This attribute can be applied to test methods or test classes to provide a custom identifier
/// that will be captured in the test metadata for session tracking and reporting purposes.
/// </summary>
/// <remarks>
/// The identifier provided through this attribute is used to uniquely identify and map test sessions.
/// This is particularly useful for tracking tests across different executions and environments.
/// </remarks>
/// <example>
/// <code>
/// [Test]
/// [Xping("homepage-title-verification")]
/// public async Task VerifyHomePageTitle()
/// {
///     // Test implementation
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class XpingAttribute : Attribute
{
    /// <summary>
    /// Gets the unique identifier for test session mapping.
    /// </summary>
    public string Identifier { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="XpingAttribute"/> class with the specified identifier.
    /// </summary>
    /// <param name="identifier">The unique identifier for test session mapping.</param>
    /// <exception cref="ArgumentException">Thrown when identifier is null, empty, or whitespace.</exception>
    public XpingAttribute(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new ArgumentException("Identifier cannot be null, empty, or whitespace.", nameof(identifier));
        }

        Identifier = identifier;
    }
}
