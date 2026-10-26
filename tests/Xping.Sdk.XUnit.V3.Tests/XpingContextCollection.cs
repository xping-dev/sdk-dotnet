/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Diagnostics.CodeAnalysis;

namespace Xping.Sdk.XUnit.V3.Tests;

/// <summary>
/// Serializes every test class that uses the static <see cref="XpingContext"/> singleton.
/// </summary>
[SuppressMessage("Design", "CA1515", Justification = "xUnit requires collection definitions to be public (xUnit1027)")]
[CollectionDefinition(Name)]
public sealed class XpingContextCollection
{
    /// <summary>The collection's name.</summary>
    public const string Name = "XpingContext";
}
