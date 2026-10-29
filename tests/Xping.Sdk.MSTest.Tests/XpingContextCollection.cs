/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.MSTest.Tests;

/// <summary>
/// Test classes that install and shut down the static <see cref="XpingContext"/>. xUnit runs classes in
/// parallel, and two of them sharing the one context would shut down each other's session.
/// </summary>
[CollectionDefinition(Name)]
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Maintainability",
    "CA1515:Consider making public types internal",
    Justification = "xUnit only discovers public collection definitions")]
public sealed class XpingContextCollection
{
    /// <summary>The collection's name.</summary>
    public const string Name = "XpingContext";
}
